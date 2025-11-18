# Model Connectors Developer Guide

This guide explains how to implement custom model connectors for the Healthcare AI Model Evaluator (HAIME) platform. Model connectors enable integration with AI models and services, allowing the Arena to generate predictions and outputs from various model endpoints.

## Overview

Model connectors act as adapters between the Arena platform and external AI models or services. They handle:

- **Authentication**: Managing credentials and API keys
- **Request Formatting**: Converting HAIME data structures to model-specific formats
- **Multimodal Support**: Processing text, images, and other data types
- **Response Parsing**: Extracting outputs and handling errors
- **Retry Logic**: Managing timeouts and transient failures

### Built-in Connectors

HAIME includes several pre-built connectors:

| Integration Type | Description | Use Case |
|-----------------|-------------|----------|
| `openai` | Azure OpenAI Chat Completions | General-purpose LLM inference |
| `openai-reasoning` | Azure OpenAI Reasoning Models | Complex reasoning tasks (o1, o3) |
| `cxrreportgen` | Azure ML Custom Endpoint | Specialized medical imaging models |
| `azure-serverless` | Azure AI Serveless Endpoints | Models deployed through Model Catalogue |
| `functionapp` | Azure Functions Runner | Custom processing pipelines |

## Main Components

### Key Interfaces and Classes

#### IModelRunner
[`IModelRunner`](../backend/src/MedBench.Core/Interfaces/IModelRunner.cs) - The core interface that all model connectors must implement. It defines methods for generating outputs and processing input data.

#### ModelRunnerBase
[`ModelRunnerBase`](../backend/src/MedBench.Core/Models/ModelRunner.cs) - Abstract base class providing common functionality including:
- Dependency injection setup (logger, settings, image service, scope factory)
- `GetBase64ImageWithType()` helper for retrieving images from blob storage
- `CombineBasePromptAndInstructions()` for prompt formatting
- Disposal pattern implementation

#### IModelRunnerFactory
[`IModelRunnerFactory`](../backend/src/MedBench.Core/Interfaces/IModelRunnerFactory.cs) - Factory interface for creating model runners based on integration type.

**Implementation**: [`ModelRunnerFactory`](../backend/src/MedBench.Core/Services/ModelRunnerFactory.cs) - Maps integration types to concrete runner implementations.

### Data Models

#### DataContent
[`DataContent`](../backend/src/MedBench.Core/Models/DataObject.cs) - Represents individual pieces of data (text, images, etc.) with fields:
- `Type`: Data type (`"text"`, `"imageurl"`, `"imagedata"`)
- `Content`: Text content or image ID
- `ContentUrl`: Optional URL
- `TotalTokens`: Token count if applicable

#### ModelOutput
[`ModelOutput`](../backend/src/MedBench.Core/Models/Trial.cs) - Represents outputs from other models (used in Arena comparisons)

#### Model
[`Model`](../backend/src/MedBench.Core/Models/Model.cs) - Defines model configuration including integration type, settings, and required parameters

## Implementing a Custom Connector

### Reference Implementations

Before creating a custom connector, review these existing implementations for patterns and best practices:

- [`OpenAIModelRunner`](../backend/src/MedBench.Core/Models/OpenAIModelRunner.cs) - Azure OpenAI integration using SDK
- [`AzureServelessEndpointRunner`](../backend/src/MedBench.Core/Models/AzureServelessEndpointRunner.cs) - Azure AI Model Catalog endpoints
- [`CXRReportGenModelRunner`](../backend/src/MedBench.Core/Models/CXRReportGenModelRunner.cs) - Custom Azure ML endpoint
- [`AzureFunctionAppRunner`](../backend/src/MedBench.Core/Models/AzureFunctionAppRunner.cs) - Async processing via Azure Functions

### Key Patterns to Follow

When reviewing these implementations, note:

1. **Constructor Pattern**: All runners receive the same parameters (settings, imageService, scopeFactory, logger, modelId)
2. **Settings Extraction**: Required parameters are extracted from the `settings` dictionary in the constructor
3. **Image Handling**: Use `GetBase64ImageWithType()` for `imageurl` type content
4. **Model Output Processing**: Handle Arena comparison mode where multiple model outputs may be provided
5. **Error Handling**: Wrap API calls in try-catch blocks with specific exception types
6. **Logging**: Use structured logging with context (ModelId, endpoint, etc.)
7. **Disposal**: Clean up HTTP clients and other resources

See [`ModelsController.cs`](../backend/src/MedBench.API/Controllers/ModelsController.cs) method for test implementation details.

## Best Practices

### Error Handling

Wrap API calls in try-catch blocks with specific exception types:

```csharp
try
{
    // Your implementation
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Network error calling model {ModelId}", ModelId);
    throw new InvalidOperationException($"Network error: {ex.Message}", ex);
}
catch (JsonException ex)
{
    _logger.LogError(ex, "Failed to parse response from model {ModelId}", ModelId);
    throw new InvalidOperationException($"Invalid response format: {ex.Message}", ex);
}
```

Reference [`AzureServelessEndpointRunner`](../backend/src/MedBench.Core/Models/AzureServelessEndpointRunner.cs) for retry logic with exponential backoff.

### Timeout Configuration

Always use timeouts for external API calls:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var response = await _client.PostAsync(_endpoint, content, cts.Token);
```

See [`AzureServelessEndpointRunner`](../backend/src/MedBench.Core/Models/AzureServelessEndpointRunner.cs) for a complete timeout implementation.

### Multimodal Best Practices

Handle different data types appropriately:

```csharp
if (input.Type.StartsWith("imageurl"))
{
    // Stored in blob, needs retrieval
    var (base64, mimeType) = await GetBase64ImageWithType(input);
}
else if (input.Type.StartsWith("imagedata"))
{
    // Direct base64 data
    var base64 = input.Content;
}
```

Reference [`OpenAIModelRunner`](../backend/src/MedBench.Core/Models/OpenAIModelRunner.cs) for comprehensive multimodal handling.

## Troubleshooting

### Common Issues

**Issue: "Unknown integration type"**
- Ensure you added your type to [`ModelRunnerFactory`](../backend/src/MedBench.Core/Services/ModelRunnerFactory.cs)
- Check that `IntegrationType` in the Model matches exactly

**Issue: "Missing required integration parameters"**
- Verify `RequiredIntegrationParameters` is updated in [`Model.cs`](../backend/src/MedBench.Core/Models/Model.cs)
- Check that all required settings are provided via UI

**Issue: Images not processing correctly**
- Ensure you're using `GetBase64ImageWithType()` for `imageurl` types
- Check that `IImageService` is injected correctly
- Verify image MIME types are supported by your model

**Issue: Timeout errors**
- Increase timeout in `CancellationTokenSource`
- Add retry logic for transient failures (see [`AzureServelessEndpointRunner`](../backend/src/MedBench.Core/Models/AzureServelessEndpointRunner.cs))
- Check network connectivity to endpoint

## See Also

- [Project Overview](project_overview.md) - Platform overview
- [Custom Evaluation Add-ons](custom_evaluation_addons.md) - Building evaluators
- [Evaluation Engine](evaluation_engine.md) - Evaluation architecture
