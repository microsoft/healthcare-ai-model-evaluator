using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Services;

/// <summary>
/// Background service that runs model migration on application startup
/// </summary>
public class ModelMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModelMigrationHostedService> _logger;

    public ModelMigrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<ModelMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Model migration hosted service starting...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var migrationService = scope.ServiceProvider.GetRequiredService<IModelMigrationService>();
            
            // Check if migration is needed first
            if (await migrationService.IsMigrationNeededAsync())
            {
                _logger.LogInformation("Model migration needed. Starting migration process...");
                
                var migratedCount = await migrationService.MigrateModelsToKeyVaultAsync();
                
                if (migratedCount > 0)
                {
                    _logger.LogInformation("Model migration completed successfully. {Count} models migrated to Key Vault.", migratedCount);
                }
                else
                {
                    _logger.LogWarning("Model migration completed with no models migrated. Check logs for details.");
                }
            }
            else
            {
                _logger.LogDebug("No model migration needed. All models are up to date.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during model migration on startup");
            // Don't throw - we don't want to prevent application startup if migration fails
        }

        _logger.LogDebug("Model migration hosted service started.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Model migration hosted service stopping...");
        return Task.CompletedTask;
    }
}