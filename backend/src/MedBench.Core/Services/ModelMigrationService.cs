using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using MedBench.Core.Helpers;

namespace MedBench.Core.Services;

/// <summary>
/// Service for migrating existing models to use Key Vault storage
/// </summary>
public class ModelMigrationService : IModelMigrationService
{
    private readonly IMongoCollection<Model> _collection;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<ModelMigrationService> _logger;

    public ModelMigrationService(
        IMongoDatabase database,
        IKeyVaultService keyVaultService,
        ILogger<ModelMigrationService> logger)
    {
        _collection = database.GetCollection<Model>("Models");
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<bool> IsMigrationNeededAsync()
    {
        try
        {
            // Check if there are any models with integration settings but no secret references
            // Use a simpler filter that MongoDB can handle better
            var filter = Builders<Model>.Filter.And(
                Builders<Model>.Filter.Or(
                    Builders<Model>.Filter.Eq(m => m.IntegrationSettings, null),
                    Builders<Model>.Filter.Size(m => m.IntegrationSettings, 0)
                ),
                Builders<Model>.Filter.Or(
                    Builders<Model>.Filter.Eq(m => m.SecretReferences, null),
                    Builders<Model>.Filter.Size(m => m.SecretReferences, 0)
                ),
                Builders<Model>.Filter.Ne(m => m.HasSecureSettings, true)
            );

            var modelsNeedingMigration = await _collection.CountDocumentsAsync(filter);
            return modelsNeedingMigration > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if migration is needed");
            return false;
        }
    }

    public async Task<int> MigrateModelsToKeyVaultAsync()
    {
        try
        {
            _logger.LogInformation("Starting model migration to Key Vault...");

            // Check if Key Vault is available first
            if (!await _keyVaultService.IsAvailableAsync())
            {
                _logger.LogWarning("Key Vault is not available. Skipping migration.");
                return 0;
            }

            // Find all models that need migration
            var filter = Builders<Model>.Filter.And(
                Builders<Model>.Filter.Ne(m => m.IntegrationSettings, null),
                Builders<Model>.Filter.Or(
                    Builders<Model>.Filter.Eq(m => m.SecretReferences, null),
                    Builders<Model>.Filter.Size(m => m.SecretReferences, 0)
                ),
                Builders<Model>.Filter.Ne(m => m.HasSecureSettings, true)
            );

            var modelsToMigrate = await _collection.Find(filter).ToListAsync();
            
            // Additional filtering in memory for models that actually have integration settings
            modelsToMigrate = modelsToMigrate
                .Where(m => m.IntegrationSettings?.Any() == true)
                .ToList();

            if (!modelsToMigrate.Any())
            {
                _logger.LogInformation("No models found that require migration.");
                return 0;
            }

            _logger.LogInformation("Found {Count} models to migrate to Key Vault", modelsToMigrate.Count);

            var migratedCount = 0;
            var failedCount = 0;

            foreach (var model in modelsToMigrate)
            {
                try
                {
                    if (await MigrateModelToKeyVaultAsync(model))
                    {
                        migratedCount++;
                        _logger.LogDebug("Successfully migrated model {ModelId} ({ModelName})", model.Id, model.Name);
                    }
                    else
                    {
                        failedCount++;
                        _logger.LogWarning("Failed to migrate model {ModelId} ({ModelName})", model.Id, model.Name);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Error migrating model {ModelId} ({ModelName})", model.Id, model.Name);
                }
            }

            _logger.LogInformation("Migration completed. Successfully migrated: {MigratedCount}, Failed: {FailedCount}", 
                migratedCount, failedCount);

            return migratedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model migration to Key Vault");
            return 0;
        }
    }

    /// <summary>
    /// Migrates a single model's integration settings to Key Vault
    /// </summary>
    private async Task<bool> MigrateModelToKeyVaultAsync(Model model)
    {
        if (model.IntegrationSettings == null || !model.IntegrationSettings.Any())
        {
            return false;
        }

        try
        {
            // Store all integration settings as secrets in Key Vault (assume all are sensitive)
            var secretReferences = new Dictionary<string, string>();
            var successfulSecrets = new List<string>();
            var failedSecrets = new List<string>();

            foreach (var (parameterName, parameterValue) in model.IntegrationSettings)
            {
                if (string.IsNullOrWhiteSpace(parameterValue))
                {
                    // Keep empty/null values in database for metadata preservation
                    continue;
                }

                var secretName = ModelSecretHelper.GenerateSecretName(model.Id, parameterName);
                
                var success = await _keyVaultService.SetSecretAsync(secretName, parameterValue);
                if (success)
                {
                    secretReferences[parameterName] = secretName;
                    successfulSecrets.Add(parameterName);
                }
                else
                {
                    failedSecrets.Add(parameterName);
                    _logger.LogWarning("Failed to store secret for model {ModelId}, parameter {Parameter}. Keeping in database.", 
                        model.Id, parameterName);
                }
            }

            // If we successfully migrated any secrets, update the model
            if (successfulSecrets.Any())
            {
                // Create display-safe integration settings with "***CONFIGURED***" placeholders
                var displaySettings = new Dictionary<string, string>();
                
                foreach (var (parameterName, parameterValue) in model.IntegrationSettings)
                {
                    if (secretReferences.ContainsKey(parameterName))
                    {
                        // Parameter was successfully migrated to Key Vault
                        displaySettings[parameterName] = "***CONFIGURED***";
                    }
                    else
                    {
                        // Parameter failed migration or was empty, keep original value
                        displaySettings[parameterName] = parameterValue;
                    }
                }

                // Update the model in the database
                model.SecretReferences = secretReferences;
                model.HasSecureSettings = true;
                model.IntegrationSettings = displaySettings;
                model.UpdatedAt = DateTime.UtcNow;

                var result = await _collection.ReplaceOneAsync(x => x.Id == model.Id, model);
                
                if (result.ModifiedCount > 0)
                {
                    _logger.LogDebug("Successfully updated model {ModelId} with {Count} secrets migrated to Key Vault", 
                        model.Id, successfulSecrets.Count);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to update model {ModelId} in database after migrating secrets", model.Id);
                    
                    // Clean up secrets since database update failed
                    await CleanupFailedMigrationSecretsAsync(secretReferences.Values);
                    return false;
                }
            }
            else if (failedSecrets.Any())
            {
                _logger.LogWarning("No secrets were successfully migrated for model {ModelId}. All {Count} parameters failed migration.", 
                    model.Id, failedSecrets.Count);
                return false;
            }
            else
            {
                _logger.LogDebug("Model {ModelId} had no non-empty integration settings to migrate", model.Id);
                return true; // Consider this a successful migration (nothing to do)
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating model {ModelId} to Key Vault", model.Id);
            return false;
        }
    }

    /// <summary>
    /// Cleans up secrets from Key Vault if database update fails
    /// </summary>
    private async Task CleanupFailedMigrationSecretsAsync(IEnumerable<string> secretNames)
    {
        foreach (var secretName in secretNames)
        {
            try
            {
                await _keyVaultService.DeleteSecretAsync(secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup secret {SecretName} after migration failure", secretName);
            }
        }
    }
}