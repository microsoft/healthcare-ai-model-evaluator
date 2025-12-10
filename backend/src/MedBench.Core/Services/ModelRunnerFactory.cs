namespace MedBench.Core.Services;   

using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using MedBench.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class ModelRunnerFactory : IModelRunnerFactory
{
    private readonly IImageService _imageService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModelRunnerFactory> _logger;
    private readonly IModelRepository _modelRepository;

    public ModelRunnerFactory(
        IImageService imageService,
        IServiceScopeFactory scopeFactory,
        ILogger<ModelRunnerFactory> logger,
        IModelRepository modelRepository)
    {
        _imageService = imageService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _modelRepository = modelRepository;
    }

    public IModelRunner CreateModelRunner(Model model)
    {
        // Load the model with secrets if needed
        var modelWithSecrets = GetModelWithSecrets(model);
        
        return modelWithSecrets.IntegrationType switch
        {
            "openai" => new OpenAIModelRunner(modelWithSecrets.IntegrationSettings, _imageService, _scopeFactory, _logger, modelWithSecrets.Id),
            "openai-reasoning" => new OpenAIReasoningModelRunner(modelWithSecrets.IntegrationSettings, _imageService, _scopeFactory, _logger, modelWithSecrets.Id),
            "cxrreportgen" => new CXRReportGenModelRunner(modelWithSecrets.IntegrationSettings, _imageService, _scopeFactory, _logger, modelWithSecrets.Id),
            "azure-serverless" => new AzureServelessEndpointRunner(modelWithSecrets.IntegrationSettings, _imageService, _scopeFactory, _logger, modelWithSecrets.Id),
            "functionapp" => new AzureFunctionAppRunner(modelWithSecrets.IntegrationSettings, _imageService, _scopeFactory, _logger, modelWithSecrets.Id),
            _ => throw new ArgumentException($"Unknown integration type: {modelWithSecrets.IntegrationType}")
        };
    }

    /// <summary>
    /// Gets model with secrets loaded for runner creation
    /// </summary>
    private Model GetModelWithSecrets(Model model)
    {
        // If the model doesn't have secure settings, use it as-is
        if (!model.HasSecureSettings)
            return model;

        try
        {
            // Load the model with secrets using the repository
            var modelWithSecrets = _modelRepository.GetByIdWithSecretsAsync(model.Id).GetAwaiter().GetResult();
            _logger.LogDebug("Successfully loaded model {ModelId} with secrets for runner creation", model.Id);
            return modelWithSecrets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load secrets for model {ModelId}. Using model without secrets.", model.Id);
            // Fall back to using the model as-is (which will have masked values)
            return model;
        }
    }
} 