using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace MedBench.Core.Repositories;

public class ModelRepository : IModelRepository
{
    private readonly IMongoCollection<Model> _collection;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<ModelRepository> _logger;

    public ModelRepository(
        IMongoDatabase database,
        IKeyVaultService keyVaultService,
        ILogger<ModelRepository> logger)
    {
        _collection = database.GetCollection<Model>("Models");
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<IEnumerable<Model>> GetAllAsync()
    {
        var models = await _collection.Find(_ => true).ToListAsync();
        
        // Return models with display-safe settings for listing
        foreach (var model in models)
        {
            if (model.HasSecureSettings)
            {
                model.IntegrationSettings = ModelSecretHelper.CreateDisplaySettings(
                    model.IntegrationSettings, 
                    model.SecretReferences);
            }
        }
        
        return models;
    }

    public async Task<Model> GetByIdAsync(string id)
    {
        var model = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (model == null)
            throw new KeyNotFoundException($"Model with ID {id} not found");
        
        // Return with display-safe settings by default
        if (model.HasSecureSettings)
        {
            model.IntegrationSettings = ModelSecretHelper.CreateDisplaySettings(
                model.IntegrationSettings, 
                model.SecretReferences);
        }
        
        return model;
    }

    public async Task<Model> GetByIdWithSecretsAsync(string id)
    {
        var model = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (model == null)
            throw new KeyNotFoundException($"Model with ID {id} not found");

        // Load secrets from Key Vault if any exist
        if (model.HasSecureSettings && model.SecretReferences.Any())
        {
            try
            {
                var secretNames = model.SecretReferences.Values;
                var secrets = await _keyVaultService.GetSecretsAsync(secretNames);
                
                model.IntegrationSettings = ModelSecretHelper.MergeSettingsWithSecrets(
                    model.IntegrationSettings,
                    secrets,
                    model.SecretReferences);
                    
                _logger.LogDebug("Loaded {Count} secrets from Key Vault for model {ModelId}", 
                    secrets.Count, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load secrets for model {ModelId}. Using non-sensitive settings only.", id);
            }
        }

        return model;
    }

    public async Task<Model> GetByIdForDisplayAsync(string id)
    {
        // This is the same as GetByIdAsync - returns display-safe settings
        return await GetByIdAsync(id);
    }

    public async Task<Model> CreateAsync(Model model)
    {
        model.Id = ObjectId.GenerateNewId().ToString();
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        
        // Handle secret storage
        await StoreSecretsAsync(model);
        
        await _collection.InsertOneAsync(model);
        return model;
    }

    public async Task<Model> UpdateAsync(Model model)
    {
        model.UpdatedAt = DateTime.UtcNow;
        
        // Get the existing model to handle secret cleanup
        var existingModel = await _collection.Find(x => x.Id == model.Id).FirstOrDefaultAsync();
        if (existingModel == null)
            throw new KeyNotFoundException($"Model with ID {model.Id} not found");
        
        // Clean up old secrets that are no longer needed
        await CleanupOldSecretsAsync(existingModel, model);
        
        // Store new/updated secrets
        await StoreSecretsAsync(model);
        
        var result = await _collection.ReplaceOneAsync(x => x.Id == model.Id, model);
        if (result.ModifiedCount == 0)
            throw new KeyNotFoundException($"Model with ID {model.Id} not found");
        return model;
    }

    public async Task DeleteAsync(string id)
    {
        // Get the model first to clean up secrets
        var model = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (model == null)
            throw new KeyNotFoundException($"Model with ID {id} not found");
        
        // Clean up secrets from Key Vault
        if (model.HasSecureSettings && model.SecretReferences.Any())
        {
            await DeleteSecretsAsync(model.SecretReferences.Values);
        }
        
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"Model with ID {id} not found");
    }

    /// <summary>
    /// Stores sensitive integration settings as secrets in Key Vault
    /// </summary>
    private async Task StoreSecretsAsync(Model model)
    {
        if (model.IntegrationSettings == null || !model.IntegrationSettings.Any())
        {
            return;
        }

        // Process incoming settings to handle "***CONFIGURED***" placeholders
        var existingSecretReferences = model.SecretReferences ?? new Dictionary<string, string>();
        var (settingsForStorage, secretReferencesToKeep) = ModelSecretHelper.ProcessIncomingSettings(
            model.IntegrationSettings, 
            existingSecretReferences);

        // If no new settings to store, just keep existing secret references
        if (!settingsForStorage.Any())
        {
            model.SecretReferences = secretReferencesToKeep;
            model.HasSecureSettings = secretReferencesToKeep.Any();
            model.IntegrationSettings = new Dictionary<string, string>();
            return;
        }

        // Treat all non-empty settings as sensitive (per requirement)
        var sensitiveSettings = settingsForStorage.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var nonSensitiveSettings = settingsForStorage.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);
        
        if (!sensitiveSettings.Any())
        {
            // No sensitive settings, just keep what we have
            model.SecretReferences = secretReferencesToKeep;
            model.HasSecureSettings = secretReferencesToKeep.Any();
            model.IntegrationSettings = nonSensitiveSettings;
            return;
        }

        // Store sensitive settings in Key Vault
        var secretReferences = new Dictionary<string, string>(secretReferencesToKeep);
        var successfulSecrets = new List<string>();

        foreach (var (parameterName, parameterValue) in sensitiveSettings)
        {
            var secretName = ModelSecretHelper.GenerateSecretName(model.Id, parameterName);
            
            try
            {
                var success = await _keyVaultService.SetSecretAsync(secretName, parameterValue);
                if (success)
                {
                    secretReferences[parameterName] = secretName;
                    successfulSecrets.Add(parameterName);
                    _logger.LogDebug("Stored secret for model {ModelId}, parameter {Parameter}", 
                        model.Id, parameterName);
                }
                else
                {
                    _logger.LogWarning("Failed to store secret for model {ModelId}, parameter {Parameter}. Will store in database instead.", 
                        model.Id, parameterName);
                    nonSensitiveSettings[parameterName] = parameterValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing secret for model {ModelId}, parameter {Parameter}. Will store in database instead.", 
                    model.Id, parameterName);
                nonSensitiveSettings[parameterName] = parameterValue;
            }
        }

        // Update model with secret references and non-sensitive settings
        model.SecretReferences = secretReferences;
        model.HasSecureSettings = secretReferences.Any();
        model.IntegrationSettings = nonSensitiveSettings;

        if (successfulSecrets.Any())
        {
            _logger.LogInformation("Stored {Count} secrets in Key Vault for model {ModelId}", 
                successfulSecrets.Count, model.Id);
        }
    }

    /// <summary>
    /// Cleans up old secrets that are no longer needed
    /// </summary>
    private async Task CleanupOldSecretsAsync(Model existingModel, Model updatedModel)
    {
        if (!existingModel.HasSecureSettings || !existingModel.SecretReferences.Any())
            return;

        // Process incoming settings to understand what should be kept
        var (settingsForStorage, secretReferencesToKeep) = ModelSecretHelper.ProcessIncomingSettings(
            updatedModel.IntegrationSettings ?? new(), 
            existingModel.SecretReferences);

        // Find secrets that are no longer referenced
        var secretsToDelete = existingModel.SecretReferences
            .Where(kv => !secretReferencesToKeep.ContainsKey(kv.Key))
            .Select(kv => kv.Value)
            .ToList();

        // Also check if any new settings would replace existing secrets
        foreach (var (parameterName, parameterValue) in settingsForStorage)
        {
            if (!string.IsNullOrWhiteSpace(parameterValue) && 
                existingModel.SecretReferences.TryGetValue(parameterName, out var existingSecretName) &&
                !secretReferencesToKeep.ContainsKey(parameterName))
            {
                secretsToDelete.Add(existingSecretName);
            }
        }

        if (secretsToDelete.Any())
        {
            await DeleteSecretsAsync(secretsToDelete.Distinct());
            _logger.LogDebug("Cleaned up {Count} old secrets for model {ModelId}", 
                secretsToDelete.Count, existingModel.Id);
        }
    }

    /// <summary>
    /// Deletes multiple secrets from Key Vault
    /// </summary>
    private async Task DeleteSecretsAsync(IEnumerable<string> secretNames)
    {
        foreach (var secretName in secretNames)
        {
            try
            {
                await _keyVaultService.DeleteSecretAsync(secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete secret {SecretName} from Key Vault", secretName);
            }
        }
    }
} 