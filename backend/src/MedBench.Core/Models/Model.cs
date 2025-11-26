using System.Globalization;

namespace MedBench.Core.Models;

public class ModelExperimentResults
{
    public double EloScore { get; set; } = 1500; // Starting ELO score
    public double AverageRating { get; set; } = 0;
    public double CorrectScore { get; set; } = 0;
    public double ValidationTime { get; set; } = 0;

    public Dictionary<string, double> SingleEvaluationScores { get; set; } = new();
}

public class Model
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ModelExperimentResults ExperimentResults { get; set; } = new();
    public Dictionary<string, ModelExperimentResults> ExperimentResultsByMetric { get; set; } = new();
    public string IntegrationType { get; set; } = string.Empty;

    public Dictionary<string, string> IntegrationSettings { get; set; } = new();

    /// <summary>
    /// Tracks which integration settings are stored as secrets in Key Vault.
    /// Key: parameter name, Value: secret name in Key Vault
    /// </summary>
    public Dictionary<string, string> SecretReferences { get; set; } = new();

    /// <summary>
    /// Indicates whether this model has any settings stored in Key Vault
    /// </summary>
    public bool HasSecureSettings { get; set; } = false;

    public double CostPerToken { get; set; } = 0;

    public double CostPerTokenOut { get; set; } = 0;

    public static readonly Dictionary<string, string[]> RequiredIntegrationParameters = new()
    {
        ["openai"] = new[] { "ENDPOINT", "API_KEY", "DEPLOYMENT" },
        ["openai-reasoning"] = new[] { "ENDPOINT", "API_KEY", "DEPLOYMENT" },
        ["cxrreportgen"] = new[] { "ENDPOINT", "API_KEY", "DEPLOYMENT", "VERSION" },
        ["azure-serverless"] = new[] { "ENDPOINT", "API_KEY" },
        ["functionapp"] = new[] { "FunctionAppType" },
    };

    public void ValidateIntegrationSettings()
    {
        if (string.IsNullOrEmpty(IntegrationType)) return;

        if (!RequiredIntegrationParameters.ContainsKey(IntegrationType))
        {
            throw new ArgumentException($"Unknown integration type: {IntegrationType}");
        }

        var requiredParams = RequiredIntegrationParameters[IntegrationType];
        var missingParams = requiredParams
            .Where(param => !IntegrationSettings.ContainsKey(param) || string.IsNullOrEmpty(IntegrationSettings[param]))
            .ToList();

        if (missingParams.Any())
        {
            throw new ArgumentException(
                $"Missing required integration parameters for {IntegrationType}: {string.Join(", ", missingParams)}. " +
                $"Required parameters are: {string.Join(", ", requiredParams)}");
        }
    }
} 