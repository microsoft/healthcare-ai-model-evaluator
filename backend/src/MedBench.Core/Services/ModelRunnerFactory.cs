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

    public ModelRunnerFactory(
        IImageService imageService,
        IServiceScopeFactory scopeFactory,
        ILogger<ModelRunnerFactory> logger)
    {
        _imageService = imageService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IModelRunner CreateModelRunner(Model model)
    {
        return model.IntegrationType switch
        {
            "openai" => new OpenAIModelRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
            "openai-reasoning" => new OpenAIReasoningModelRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
            "cxrreportgen" => new CXRReportGenModelRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
            "deepseek" => new AzureServelessEndpointRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
            "phi4" => new AzureServelessEndpointRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
            "functionapp" => new AzureFunctionAppRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
            _ => throw new ArgumentException($"Unknown integration type: {model.IntegrationType}")
        };
    }
} 