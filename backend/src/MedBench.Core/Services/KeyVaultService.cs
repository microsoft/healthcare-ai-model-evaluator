using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Azure.Core;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Services;

/// <summary>
/// Key Vault service implementation with caching and graceful failure handling
/// </summary>
public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient? _secretClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeyVaultService> _logger;
    private readonly TimeSpan _cacheExpiration;
    private readonly bool _isAvailable;
    private readonly string _keyVaultName;

    public KeyVaultService(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<KeyVaultService> logger)
    {
        _cache = cache;
        _logger = logger;
        _cacheExpiration = TimeSpan.FromMinutes(30); // Cache secrets for 30 minutes

        // Get Key Vault configuration
        _keyVaultName = configuration["KeyVault:Name"] ?? 
                       Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_NAME") ?? 
                       string.Empty;

        if (string.IsNullOrEmpty(_keyVaultName))
        {
            _logger.LogWarning("Key Vault name not configured. Secret storage will be disabled.");
            _isAvailable = false;
            return;
        }

        try
        {
            var keyVaultUri = new Uri($"https://{_keyVaultName}.vault.azure.net/");
            
            // Use DefaultAzureCredential for authentication (works in both dev and production)
            var credential = new DefaultAzureCredential();
            
            _secretClient = new SecretClient(keyVaultUri, credential);
            _isAvailable = true;
            
            _logger.LogInformation("Key Vault service initialized successfully for vault: {KeyVaultName}", _keyVaultName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Key Vault service for vault: {KeyVaultName}", _keyVaultName);
            _isAvailable = false;
        }
    }

    public async Task<bool> SetSecretAsync(string secretName, string secretValue)
    {
        if (!_isAvailable || _secretClient == null)
        {
            _logger.LogWarning("Key Vault not available. Cannot set secret: {SecretName}", secretName);
            return false;
        }

        try
        {
            await _secretClient.SetSecretAsync(secretName, secretValue);
            
            // Update cache with new value
            var cacheKey = GetCacheKey(secretName);
            _cache.Set(cacheKey, secretValue, _cacheExpiration);
            
            _logger.LogDebug("Secret set successfully: {SecretName}", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret in Key Vault: {SecretName}", secretName);
            return false;
        }
    }

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (!_isAvailable || _secretClient == null)
        {
            _logger.LogWarning("Key Vault not available. Cannot retrieve secret: {SecretName}", secretName);
            return null;
        }

        var cacheKey = GetCacheKey(secretName);
        
        // Check cache first
        if (_cache.TryGetValue(cacheKey, out string? cachedValue))
        {
            _logger.LogDebug("Secret retrieved from cache: {SecretName}", secretName);
            return cachedValue;
        }

        try
        {
            var secret = await _secretClient.GetSecretAsync(secretName);
            var value = secret.Value.Value;
            
            // Cache the value
            _cache.Set(cacheKey, value, _cacheExpiration);
            
            _logger.LogDebug("Secret retrieved from Key Vault: {SecretName}", secretName);
            return value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret not found in Key Vault: {SecretName}", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret from Key Vault: {SecretName}", secretName);
            return null;
        }
    }

    public async Task<Dictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretNames)
    {
        var result = new Dictionary<string, string>();
        
        if (!_isAvailable || _secretClient == null)
        {
            _logger.LogWarning("Key Vault not available. Cannot retrieve secrets");
            return result;
        }

        var secretNamesList = secretNames.ToList();
        var uncachedSecrets = new List<string>();
        
        // Check cache first
        foreach (var secretName in secretNamesList)
        {
            var cacheKey = GetCacheKey(secretName);
            if (_cache.TryGetValue(cacheKey, out string? cachedValue) && cachedValue != null)
            {
                result[secretName] = cachedValue;
            }
            else
            {
                uncachedSecrets.Add(secretName);
            }
        }

        // Retrieve uncached secrets
        var tasks = uncachedSecrets.Select(async secretName =>
        {
            try
            {
                var secret = await _secretClient.GetSecretAsync(secretName);
                var value = secret.Value.Value;
                
                // Cache the value
                var cacheKey = GetCacheKey(secretName);
                _cache.Set(cacheKey, value, _cacheExpiration);
                
                return new KeyValuePair<string, string?>(secretName, value);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogDebug("Secret not found in Key Vault: {SecretName}", secretName);
                return new KeyValuePair<string, string?>(secretName, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret from Key Vault: {SecretName}", secretName);
                return new KeyValuePair<string, string?>(secretName, null);
            }
        });

        var retrievedSecrets = await Task.WhenAll(tasks);
        
        foreach (var secretResult in retrievedSecrets)
        {
            if (secretResult.Value != null)
            {
                result[secretResult.Key] = secretResult.Value;
            }
        }

        _logger.LogDebug("Retrieved {Count} out of {Total} secrets from Key Vault", result.Count, secretNamesList.Count);
        return result;
    }

    public async Task<bool> DeleteSecretAsync(string secretName)
    {
        if (!_isAvailable || _secretClient == null)
        {
            _logger.LogWarning("Key Vault not available. Cannot delete secret: {SecretName}", secretName);
            return false;
        }

        try
        {
            await _secretClient.StartDeleteSecretAsync(secretName);
            
            // Remove from cache
            var cacheKey = GetCacheKey(secretName);
            _cache.Remove(cacheKey);
            
            _logger.LogDebug("Secret deleted successfully: {SecretName}", secretName);
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret not found for deletion: {SecretName}", secretName);
            return true; // Consider it successful if the secret doesn't exist
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret from Key Vault: {SecretName}", secretName);
            return false;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        if (!_isAvailable || _secretClient == null)
        {
            return false;
        }

        try
        {
            // Try to enumerate secrets to verify connectivity
            var pages = _secretClient.GetPropertiesOfSecretsAsync().AsPages();
            await foreach (var page in pages)
            {
                // Successfully got at least one page, Key Vault is available
                break;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key Vault availability check failed");
            return false;
        }
    }

    public void ClearSecretFromCache(string secretName)
    {
        var cacheKey = GetCacheKey(secretName);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Cleared secret from cache: {SecretName}", secretName);
    }

    public void ClearCache()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Clear();
        }
        _logger.LogDebug("Cleared all secrets from cache");
    }

    private static string GetCacheKey(string secretName)
    {
        return $"kv_secret_{secretName}";
    }
}