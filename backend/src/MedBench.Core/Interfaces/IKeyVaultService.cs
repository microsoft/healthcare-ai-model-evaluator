namespace MedBench.Core.Interfaces;

/// <summary>
/// Service for managing secrets in Azure Key Vault with caching and graceful failure handling
/// </summary>
public interface IKeyVaultService
{
    /// <summary>
    /// Stores a secret in Key Vault
    /// </summary>
    /// <param name="secretName">Name of the secret</param>
    /// <param name="secretValue">Value to store</param>
    /// <returns>True if successful, false if failed gracefully</returns>
    Task<bool> SetSecretAsync(string secretName, string secretValue);

    /// <summary>
    /// Retrieves a secret from Key Vault with caching
    /// </summary>
    /// <param name="secretName">Name of the secret to retrieve</param>
    /// <returns>Secret value if found and accessible, null if not found or failed gracefully</returns>
    Task<string?> GetSecretAsync(string secretName);

    /// <summary>
    /// Retrieves multiple secrets in a single operation for efficiency
    /// </summary>
    /// <param name="secretNames">Names of the secrets to retrieve</param>
    /// <returns>Dictionary of secret names to values. Missing or failed secrets will not be included.</returns>
    Task<Dictionary<string, string>> GetSecretsAsync(IEnumerable<string> secretNames);

    /// <summary>
    /// Deletes a secret from Key Vault
    /// </summary>
    /// <param name="secretName">Name of the secret to delete</param>
    /// <returns>True if successful or if secret didn't exist, false if failed gracefully</returns>
    Task<bool> DeleteSecretAsync(string secretName);

    /// <summary>
    /// Checks if the Key Vault service is available and accessible
    /// </summary>
    /// <returns>True if Key Vault is accessible, false otherwise</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Clears the cache for a specific secret
    /// </summary>
    /// <param name="secretName">Name of the secret to clear from cache</param>
    void ClearSecretFromCache(string secretName);

    /// <summary>
    /// Clears all cached secrets
    /// </summary>
    void ClearCache();
}