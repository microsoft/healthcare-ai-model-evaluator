using MedBench.Core.Interfaces;
using MedBenchUser = MedBench.Core.Models.User;

namespace MedBench.API.Services;

public class RootAdminSeeder : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IConfiguration _config;
	private readonly ILogger<RootAdminSeeder> _logger;

	public RootAdminSeeder(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<RootAdminSeeder> logger)
	{
		_scopeFactory = scopeFactory;
		_config = config;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var email = _config["RootAdmin:Email"]?.Trim().ToLowerInvariant();
		var name = _config["RootAdmin:Name"]?.Trim();
		var password = _config["RootAdmin:Password"];

		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
		{
			_logger.LogInformation("Root admin bootstrap not configured. Skipping.");
			return;
		}

		using var scope = _scopeFactory.CreateScope();
		var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
		var auth = scope.ServiceProvider.GetRequiredService<ILocalAuthService>();

		var existing = await users.FindByEmailAsync(email);
		if (existing == null)
		{
			var newUser = new MedBenchUser
			{
				Id = Guid.NewGuid().ToString(),
				Name = name,
				Email = email,
				Roles = new List<string> { "Administrator" },
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow,
				IsModelReviewer = false,
				ModelId = null,
				Expertise = null,
				Stats = new Dictionary<string, string>()
			};

			await users.CreateAsync(newUser);
			await auth.SetPasswordForUserAsync(email, password);
			_logger.LogInformation("Root admin user created: {Email}", email);
			return;
		}

		var roles = existing.Roles ?? new List<string>();
		if (!roles.Contains("Administrator"))
		{
			roles.Add("Administrator");
			existing.Roles = roles;
			existing.UpdatedAt = DateTime.UtcNow;
			await users.UpdateAsync(existing);
			_logger.LogInformation("Root admin role added to existing user: {Email}", email);
		}

		if (string.IsNullOrWhiteSpace(existing.PasswordHash))
		{
			await auth.SetPasswordForUserAsync(email, password);
			_logger.LogInformation("Root admin password set for existing user: {Email}", email);
		}
	}
}
