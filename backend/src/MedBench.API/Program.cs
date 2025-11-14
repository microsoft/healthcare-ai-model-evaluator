using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Repositories;
using MedBench.Core.Services;
using OpenApiInfo = Microsoft.OpenApi.Models.OpenApiInfo;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using MedBench.API.Middleware;
using Azure.Storage.Blobs;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure MongoDB conventions to ignore extra elements
var conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
ConventionRegistry.Register("IgnoreExtraElements", conventionPack, type => true);

builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.Warning);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>();
    
    // Disable service provider validation to avoid DI scope issues during development
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = false;
        options.ValidateOnBuild = false;
    });
}

// Add authentication for both AAD and Local JWT using a policy scheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Dynamic";
    options.DefaultAuthenticateScheme = "Dynamic";
    options.DefaultChallengeScheme = "Dynamic";
})
    .AddPolicyScheme("Dynamic", "Dynamic", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var auth = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return "AzureAd"; // default
            var token = auth.Substring("Bearer ".Length).Trim();
            try
            {
                var parts = token.Split('.');
                if (parts.Length == 3)
                {
                    var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[1])));
                    if (payloadJson.Contains("login.microsoftonline.com") || payloadJson.Contains("sts.windows.net"))
                        return "AzureAd";
                }
            }
            catch { }
            return "LocalJwt";
        };
    })
    .AddJwtBearer("AzureAd", options =>
    {
        var tenantId = builder.Configuration["AzureAd:TenantId"];
        var audience = builder.Configuration["AzureAd:Audience"];
        options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer("LocalJwt", options =>
    {
        var secret = builder.Configuration["LocalAuth:JwtSecret"];
        var issuer = builder.Configuration["LocalAuth:Issuer"] ?? "medbench-local";
        var audience = builder.Configuration["LocalAuth:Audience"] ?? "medbench-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret ?? string.Empty)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

static string PadBase64(string input)
{
    return input.PadRight(input.Length + (4 - input.Length % 4) % 4, '=');
}

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        var configured = builder.Configuration["AllowedOrigins"];
        var origins = (configured ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // Fallback to common local dev origins if none configured
        if (origins.Count == 0)
        {
            origins = new List<string>
            {
                "http://localhost:3000",
                "https://localhost:3000",
                "http://localhost:5000",
                "https://localhost:5000",
                "http://localhost:5173",
                "https://localhost:5173",
                "http://localhost:5174",
                "https://localhost:5174",
                "https://zealous-bay-0c086891e.6.azurestaticapps.net"
            };
        }

        policy
            .WithOrigins(origins.ToArray())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            // Cache preflight for 10 minutes to reduce OPTIONS traffic in dev
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
    options.AddPolicy("RequireAdministratorRole", policy =>
        policy.RequireRole("Administrator"));
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Configure request size limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue; // or a specific size like 209715200 for 200MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue; // or a specific size like 209715200 for 200MB
});

// Add after builder.Services.AddControllers()
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = int.MaxValue; // or a specific size
});

// Add endpoints to the container.
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with OAuth
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Healthcare AI Model Evaluator API", Version = "v1" });
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    { $"api://{builder.Configuration["AzureAd:ClientId"]}/access_as_user", "Access as user" }
                }
            }
        }
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
            },
            new[] { $"api://{builder.Configuration["AzureAd:ClientId"]}/access_as_user" }
        }
    });
});

// Add logging to debug connection issues
builder.Services.AddSingleton(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("COSMOSDB_CONNECTION_STRING") 
        ?? builder.Configuration["CosmosDb:ConnectionString"];
        
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("MongoDB connection string not found");
    }
    
    try {
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(builder.Configuration["CosmosDb:DatabaseName"]);
        // Test the connection
        database.RunCommandAsync((Command<BsonDocument>)"{ping:1}").Wait();

        // After getting the database instance
        var collections = new[] { "Users", "Models", "Experiments", "ClinicalTasks", "TestScenarios", "DataObjects" };
        foreach (var collectionName in collections)
        {
            if (!database.ListCollectionNames().ToList().Contains(collectionName))
            {
                database.CreateCollection(collectionName);
            }
        }

        return database;
    }
    catch (Exception ex)
    {
        // Log the exception details
        Console.WriteLine($"Failed to connect to MongoDB: {ex}");
        throw;
    }
});

// Remove the CosmosDbContext registration and replace with MongoDB collections
builder.Services.AddSingleton(sp =>
{
    var database = sp.GetRequiredService<IMongoDatabase>();
    return database.GetCollection<MedBench.Core.Models.User>(builder.Configuration["CosmosDb:ContainerName"]);
});

// Update the MongoDB Client registration to use the existing connection string
builder.Services.AddSingleton<IMongoClient>(sp => 
{
    var connectionString = Environment.GetEnvironmentVariable("COSMOSDB_CONNECTION_STRING") 
        ?? builder.Configuration["CosmosDb:ConnectionString"];
        
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("MongoDB connection string not found");
    }
    
    return new MongoClient(connectionString);
});

// Register repositories
builder.Services.AddScoped<IUserRepository, MedBench.Core.Repositories.UserRepository>();
builder.Services.AddScoped<IDataSetRepository, MedBench.Core.Repositories.DataSetRepository>();
builder.Services.AddScoped<IExperimentRepository, MedBench.Core.Repositories.ExperimentRepository>();
builder.Services.AddScoped<ITestScenarioRepository, TestScenarioRepository>();
builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<IClinicalTaskRepository, ClinicalTaskRepository>();
builder.Services.AddScoped<IDataObjectRepository, DataObjectRepository>();
builder.Services.AddScoped<ITrialRepository, TrialRepository>();
builder.Services.AddScoped<IImageRepository, MedBench.Core.Repositories.ImageRepository>();


// Add Azure Blob Storage
builder.Services.AddSingleton(x => 
    new BlobServiceClient(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
        ?? builder.Configuration["AzureStorage:ConnectionString"])
);

// Register Image Service
builder.Services.AddScoped<IImageService, ImageService>();


// Also register the background service
builder.Services.AddSingleton<IExperimentProcessingService, ExperimentProcessingService>();
builder.Services.AddHostedService<ExperimentProcessingService>();

// Add this with your other service registrations
builder.Services.AddScoped<IModelRunnerFactory, ModelRunnerFactory>();

// Add these with your other service registrations
builder.Services.AddScoped<StatCalculatorService>();

// Model runners are created by the factory, not directly injected
BsonClassMap.RegisterClassMap<TestScenario>(cm => {
    cm.AutoMap();
    cm.SetIgnoreExtraElements(true);
});

// Add this to the service registration section:
builder.Services.AddScoped<IDataFileService, DataFileService>();

// Local auth and email services (from Core)
builder.Services.AddScoped<ILocalAuthService, MedBench.Core.Services.LocalAuthService>();
builder.Services.AddScoped<IEmailService, MedBench.Core.Services.EmailService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare AI Model Evaluator API v1");
        c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
        c.OAuthUsePkce();
        c.OAuthScopeSeparator(" ");
    });
}

// Important: run CORS before HTTPS redirection so redirect responses include CORS headers
app.UseCors("default");

app.UseHttpsRedirection();

// Configure static file serving - order matters!
// First, serve files from the default wwwroot
app.UseStaticFiles();

// Then, serve React app files from wwwroot/webapp at the /webapp path
var webappPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "webapp");
Console.WriteLine($"Setting up static files for webapp at: {webappPath}");
Console.WriteLine($"Directory exists: {Directory.Exists(webappPath)}");

if (Directory.Exists(webappPath))
{
    var assetsPath = Path.Combine(webappPath, "assets");
    Console.WriteLine($"Assets directory exists: {Directory.Exists(assetsPath)}");
    if (Directory.Exists(assetsPath))
    {
        var files = Directory.GetFiles(assetsPath, "*.js");
        Console.WriteLine($"JS files in assets: {string.Join(", ", files.Select(Path.GetFileName))}");
    }
}

// Configure static files with explicit options
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webappPath),
    RequestPath = "/webapp",
    OnPrepareResponse = ctx =>
    {
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserIdMiddleware>();

app.MapControllers();

// Handle common files that might be requested from root and redirect to webapp
app.MapGet("/{filename}", (HttpContext context, string filename) =>
{
    var commonFiles = new[] { "favicon.ico", "logo.png", "favicon.svg", "apple-touch-icon.png", "manifest.json", "robots.txt" };
    
    if (commonFiles.Contains(filename.ToLowerInvariant()))
    {
        var webappFile = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "webapp", filename);
        if (File.Exists(webappFile))
        {
            Console.WriteLine($"Redirecting {filename} from root to webapp");
            context.Response.Redirect($"/webapp/{filename}", permanent: true);
            return;
        }
    }
    
    // If not a common file, return 404
    context.Response.StatusCode = 404;
});

// Handle the root /webapp route specifically
app.MapGet("/webapp", async (HttpContext context) =>
{
    var indexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "webapp", "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("React app not found.");
    }
});

// Use MapFallback for React app routes - this has the lowest priority and only handles navigation routes
app.MapFallback("/webapp/{*path}", async (HttpContext context, ILogger<Program> logger) =>
{
    var requestPath = context.Request.Path.Value ?? "";
    
    // Log for debugging
    logger.LogDebug("Fallback React route received request: {RequestPath}", requestPath);
    
    // If the request has a file extension, it's likely a static file that wasn't found
    // Let it 404 instead of serving the React app
    if (Path.HasExtension(requestPath))
    {
        logger.LogDebug("File request with extension, letting it 404: {RequestPath}", requestPath);
        context.Response.StatusCode = 404;
        return;
    }
    
    // Serve index.html for React app navigation routes
    var indexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "webapp", "index.html");
    logger.LogDebug("Serving index.html for React route: {RequestPath}", requestPath);
    
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("React app not found.");
    }
});

// Add before app.Run()
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else 
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var error = context.Features.Get<IExceptionHandlerFeature>();
            if (error != null)
            {
                await context.Response.WriteAsJsonAsync(new 
                { 
                    Message = "An error occurred.",
                    Detail = error.Error.Message
                });
            }
        });
    });
}


app.Run(); 