using System.Text.RegularExpressions;

namespace MedBench.Core.Helpers;

/// <summary>
/// Helper class for managing model secrets in Key Vault
/// </summary>
public static class ModelSecretHelper
{
    /// <summary>
    /// Parameters that should be considered sensitive and stored in Key Vault
    /// </summary>
    public static readonly HashSet<string> SensitiveParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApiKey",
        "API_KEY", 
        "Secret",
        "Password",
        "Token",
        "Key"
    };

    /// <summary>
    /// Generates a Key Vault secret name for a model's integration setting
    /// </summary>
    /// <param name="modelId">The model ID</param>
    /// <param name="parameterName">The parameter name</param>
    /// <returns>A Key Vault-compatible secret name</returns>
    public static string GenerateSecretName(string modelId, string parameterName)
    {
        // Key Vault secret names must be 1-127 characters long and contain only alphanumeric characters and hyphens
        var sanitizedModelId = SanitizeForKeyVault(modelId);
        var sanitizedParamName = SanitizeForKeyVault(parameterName);
        
        return $"model-{sanitizedModelId}-{sanitizedParamName}";
    }

    /// <summary>
    /// Determines if a parameter should be stored as a secret in Key Vault
    /// </summary>
    /// <param name="parameterName">The parameter name to check</param>
    /// <returns>True if the parameter should be stored as a secret</returns>
    public static bool IsSensitiveParameter(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            return false;

        return SensitiveParameters.Any(sensitive => 
            parameterName.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Separates integration settings into sensitive and non-sensitive parameters
    /// </summary>
    /// <param name="integrationSettings">All integration settings</param>
    /// <returns>Tuple of (sensitiveSettings, nonSensitiveSettings)</returns>
    public static (Dictionary<string, string> sensitive, Dictionary<string, string> nonSensitive) 
        SeparateSettings(Dictionary<string, string> integrationSettings)
    {
        var sensitive = new Dictionary<string, string>();
        var nonSensitive = new Dictionary<string, string>();

        foreach (var (key, value) in integrationSettings)
        {
            if (IsSensitiveParameter(key))
            {
                sensitive[key] = value;
            }
            else
            {
                nonSensitive[key] = value;
            }
        }

        return (sensitive, nonSensitive);
    }

    /// <summary>
    /// Merges non-sensitive settings with secrets retrieved from Key Vault
    /// </summary>
    /// <param name="nonSensitiveSettings">Settings stored in the database</param>
    /// <param name="secrets">Secrets retrieved from Key Vault</param>
    /// <param name="secretReferences">Mapping of parameter names to secret names</param>
    /// <returns>Complete integration settings dictionary</returns>
    public static Dictionary<string, string> MergeSettingsWithSecrets(
        Dictionary<string, string> nonSensitiveSettings,
        Dictionary<string, string> secrets,
        Dictionary<string, string> secretReferences)
    {
        Console.WriteLine($"Non-sensitive settings: {string.Join(", ", nonSensitiveSettings.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        Console.WriteLine($"Secrets: {string.Join(", ", secrets.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        Console.WriteLine($"Secret references: {string.Join(", ", secretReferences.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        
        var result = new Dictionary<string, string>(nonSensitiveSettings);

        foreach (var (parameterName, secretName) in secretReferences)
        {
            if (secrets.TryGetValue(secretName, out var secretValue))
            {
                result[parameterName] = secretValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a display-safe version of integration settings for frontend use
    /// Shows configured status without revealing actual values
    /// </summary>
    /// <param name="nonSensitiveSettings">Non-sensitive settings to include as-is</param>
    /// <param name="secretReferences">Secret references to mark as configured</param>
    /// <returns>Settings safe for frontend display</returns>
    public static Dictionary<string, string> CreateDisplaySettings(
        Dictionary<string, string> nonSensitiveSettings,
        Dictionary<string, string> secretReferences)
    {
        var result = new Dictionary<string, string>(nonSensitiveSettings);

        foreach (var parameterName in secretReferences.Keys)
        {
            result[parameterName] = "***CONFIGURED***";
        }

        return result;
    }

    /// <summary>
    /// Checks if a parameter is configured (either has a value or is stored in Key Vault)
    /// </summary>
    /// <param name="parameterName">Name of the parameter</param>
    /// <param name="integrationSettings">Current integration settings</param>
    /// <param name="secretReferences">Secret references mapping</param>
    /// <returns>True if the parameter is configured</returns>
    public static bool IsParameterConfigured(
        string parameterName, 
        Dictionary<string, string>? integrationSettings,
        Dictionary<string, string>? secretReferences)
    {
        // Check if it's stored in Key Vault
        if (secretReferences?.ContainsKey(parameterName) == true)
        {
            return true;
        }

        // Check if it has a non-empty value in integration settings
        if (integrationSettings?.TryGetValue(parameterName, out var value) == true)
        {
            return !string.IsNullOrWhiteSpace(value) && value != "***CONFIGURED***";
        }

        return false;
    }

    /// <summary>
    /// Gets the missing required parameters for a given integration type
    /// </summary>
    /// <param name="integrationType">The integration type</param>
    /// <param name="integrationSettings">Current integration settings</param>
    /// <param name="secretReferences">Secret references mapping</param>
    /// <returns>List of missing required parameters</returns>
    public static List<string> GetMissingRequiredParameters(
        string integrationType,
        Dictionary<string, string>? integrationSettings,
        Dictionary<string, string>? secretReferences)
    {
        if (string.IsNullOrEmpty(integrationType))
        {
            return new List<string>();
        }

        if (!Model.RequiredIntegrationParameters.TryGetValue(integrationType, out var requiredParams))
        {
            return new List<string>();
        }

        return requiredParams
            .Where(param => !IsParameterConfigured(param, integrationSettings, secretReferences))
            .ToList();
    }

    /// <summary>
    /// Processes incoming integration settings from the frontend, preserving existing Key Vault secrets
    /// when they are marked as "***CONFIGURED***"
    /// </summary>
    /// <param name="incomingSettings">Settings from the frontend</param>
    /// <param name="existingSecretReferences">Existing secret references</param>
    /// <param name="keyVaultSecrets">Actual secrets loaded from Key Vault (if available)</param>
    /// <returns>Tuple of (settingsForStorage, secretReferencesToKeep)</returns>
    public static (Dictionary<string, string> settingsForStorage, Dictionary<string, string> secretReferencesToKeep) 
        ProcessIncomingSettings(
            Dictionary<string, string> incomingSettings,
            Dictionary<string, string> existingSecretReferences,
            Dictionary<string, string>? keyVaultSecrets = null)
    {
        var settingsForStorage = new Dictionary<string, string>();
        var secretReferencesToKeep = new Dictionary<string, string>();

        foreach (var (parameterName, parameterValue) in incomingSettings)
        {
            if (parameterValue == "***CONFIGURED***")
            {
                // This parameter was already configured in Key Vault and user didn't change it
                if (existingSecretReferences.TryGetValue(parameterName, out var secretName))
                {
                    secretReferencesToKeep[parameterName] = secretName;
                    // Don't include in settingsForStorage - it stays in Key Vault
                }
                else
                {
                    // This shouldn't happen, but if it does, treat it as an empty value
                    settingsForStorage[parameterName] = "";
                }
            }
            else
            {
                // This is a new or updated value
                settingsForStorage[parameterName] = parameterValue;
            }
        }

        return (settingsForStorage, secretReferencesToKeep);
    }

    /// <summary>
    /// Sanitizes a string to be compatible with Key Vault secret naming requirements
    /// </summary>
    /// <param name="input">Input string to sanitize</param>
    /// <returns>Sanitized string suitable for Key Vault</returns>
    private static string SanitizeForKeyVault(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "unknown";

        // Replace invalid characters with hyphens
        var sanitized = Regex.Replace(input, @"[^a-zA-Z0-9-]", "-");
        
        // Remove consecutive hyphens
        sanitized = Regex.Replace(sanitized, @"-+", "-");
        
        // Remove leading/trailing hyphens
        sanitized = sanitized.Trim('-');
        
        // Ensure it's not empty and not too long
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "unknown";
        
        if (sanitized.Length > 50) // Leave room for prefixes
            sanitized = sanitized[..50].TrimEnd('-');
        
        return sanitized.ToLowerInvariant();
    }
}