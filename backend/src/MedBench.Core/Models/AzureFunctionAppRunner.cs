using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;
using MedBench.Core.Interfaces;
using System.Text.Json;
using System.Threading;
using Azure;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace MedBench.Core.Models
{
    public class AzureFunctionAppRunner : ModelRunnerBase, IModelRunner
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _functionAppType;
        private readonly int _timeoutSeconds;

        public AzureFunctionAppRunner(
            Dictionary<string, string> settings,
            IImageService imageService,
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            string modelId) : base(settings, imageService, scopeFactory, logger, modelId)
        {
            var connectionString = GetStorageConnectionString();
            _blobServiceClient = new BlobServiceClient(connectionString);
            
            // Get function app specific parameters
            _functionAppType = GetParameter(FunctionAppRunnerSettings.FunctionAppType);
            _timeoutSeconds = int.Parse(GetParameter(FunctionAppRunnerSettings.TimeoutSeconds) ?? "300");
        }

        public override async Task<string> GenerateOutput(string prompt, List<DataContent> inputData, List<ModelOutput> outputData)
        {
            // Get clinical task ID from settings if provided
            var clinicalTaskId = _settings.GetValueOrDefault(FunctionAppRunnerSettings.ClinicalTaskId, "");
            return await GenerateOutput(prompt, "", inputData, outputData, clinicalTaskId);
        }

        public override Task<string> GenerateOutput(string basePrompt, string outputInstructions, List<DataContent> inputData, List<ModelOutput> outputData)
        {
            // Get clinical task ID from settings if provided
            var clinicalTaskId = _settings.GetValueOrDefault(FunctionAppRunnerSettings.ClinicalTaskId, "");
            return GenerateOutput(basePrompt, outputInstructions, inputData, outputData, clinicalTaskId);
        }

        public async Task<string> GenerateOutput(string basePrompt, string outputInstructions, List<DataContent> inputData, List<ModelOutput> outputData, string clinicalTaskId)
        {
            try
            {
                // Create job data structure similar to metrics workflow
                var jobData = new
                {
                    model_run = await CreateModelRunData(basePrompt, outputInstructions, inputData, outputData, clinicalTaskId),
                    function_type = _functionAppType,
                    job_id = Guid.NewGuid().ToString(),
                    created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    base_prompt = basePrompt,
                    output_instructions = outputInstructions
                };

                // Determine containers based on function app type
                var inputContainer = GetInputContainer(_functionAppType);
                var outputContainer = GetOutputContainer(_functionAppType);
                
                // Upload job to appropriate container
                var jobId = jobData.job_id;
                var blobName = $"{_functionAppType}_job_{jobId}.json";
                
                await UploadJobData(inputContainer, blobName, jobData);
                
                // Wait for and retrieve results
                return await WaitForResults(outputContainer, blobName, jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AzureFunctionAppRunner.GenerateOutput");
                throw;
            }
        }

        private string GetInputContainer(string functionType) => functionType switch
        {
            "evaluator" => "evaluatorjobs",
            _ => throw new ArgumentException($"Unknown function type: {functionType}")
        };

        private string GetOutputContainer(string functionType) => functionType switch
        {
            "evaluator" => "evaluatorresults",
            _ => throw new ArgumentException($"Unknown function type: {functionType}")
        };

        private async Task UploadJobData(string containerName, string blobName, object jobData)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            var jsonData = JsonSerializer.Serialize(jobData, new JsonSerializerOptions { WriteIndented = true });
            await blobClient.UploadAsync(BinaryData.FromString(jsonData), overwrite: true);
            
            _logger.LogInformation($"Uploaded job to {containerName}/{blobName}");
        }

        private async Task<string> WaitForResults(string containerName, string blobName, string jobId)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var resultsBlobName = $"{blobName}-results.json"; // Function app appends -results.json
            var resultsBlobClient = containerClient.GetBlobClient(resultsBlobName);

            var timeout = TimeSpan.FromSeconds(_timeoutSeconds);
            var start = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(2);

            while (DateTime.UtcNow - start < timeout)
            {
                try
                {
                    if (await resultsBlobClient.ExistsAsync())
                    {
                        var response = await resultsBlobClient.DownloadContentAsync();
                        var resultJson = response.Value.Content.ToString();
                        
                        try
                        {
                            var result = JsonSerializer.Deserialize<JsonElement>(resultJson);
                            
                            // Check if there was an error
                            if (result.TryGetProperty("error", out var errorProp))
                            {
                                var errorMessage = errorProp.GetString() ?? "Unknown function app error";
                                _logger.LogError($"Function app returned error: {errorMessage}");
                                throw new InvalidOperationException($"Function app processing failed: {errorMessage}");
                            }
                            
                            // Extract the generated output - function apps must return output in "output" field
                            if (result.TryGetProperty("output", out var outputProp))
                            {
                                return outputProp.GetString() ?? "Empty output returned from function app";
                            }
                            
                            // If no output field, this is an error condition
                            _logger.LogError($"Function app returned response without 'output' field: {resultJson}");
                            throw new InvalidOperationException("Function app response missing required 'output' field");
                        }
                        catch (JsonException ex)
                        {
                            // Handle non-JSON responses gracefully
                            _logger.LogWarning(ex, $"Function app returned non-JSON response: {resultJson}");
                            return resultJson;
                        }
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Blob doesn't exist yet, continue polling
                }

                await Task.Delay(pollInterval);
            }

            throw new TimeoutException($"Function app job {jobId} timed out after {_timeoutSeconds} seconds");
        }

        private async Task<object> CreateModelRunData(string prompt, List<DataContent> inputData, List<ModelOutput> outputData, string clinicalTaskId)
        {
            // For backward compatibility, treat prompt as basePrompt with no output instructions
            return await CreateModelRunData(prompt, "", inputData, outputData, clinicalTaskId);
        }

        private async Task<object> CreateModelRunData(string basePrompt, string outputInstructions, List<DataContent> inputData, List<ModelOutput> outputData, string clinicalTaskId)
        {
            // Create structure compatible with Python function app expectations based on medbench schema
            var inputContent = new List<object>();
            
            // Combine base prompt and output instructions into a single text content object
            var combinedPrompt = CombineBasePromptAndInstructions(basePrompt, outputInstructions);
            inputContent.Add(new
            {
                type = "Text",
                data = combinedPrompt,
                location = (string?)null,
                metadata = (object?)null,
                highlighted_segments = new object[] { }
            });

            // Process input data
            foreach (var input in inputData)
            {
                if (input.Type.StartsWith("imageurl"))
                {
                    try
                    {
                        var (base64Image, mimeType) = await GetBase64ImageWithType(input);
                        inputContent.Add(new
                        {
                            type = "Image",
                            data = $"data:{mimeType};base64,{base64Image}",
                            location = (string?)null,
                            metadata = new
                            {
                                name = Path.GetFileName(input.Content) ?? "image",
                            },
                            highlighted_segments = new object[] { }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error processing image {input.Content}");
                        inputContent.Add(new
                        {
                            type = "Text",
                            data = "Error loading image",
                            location = (string?)null,
                            metadata = (object?)null,
                            highlighted_segments = new object[] { }
                        });
                    }
                }
                else
                {
                    inputContent.Add(new
                    {
                        type = "Text",
                        data = input.Content,
                        location = (string?)null,
                        metadata = (object?)null,
                        highlighted_segments = new object[] { }
                    });
                }
            }

            // Generate deterministic input ID based on input content
            var inputId = GenerateInputId(combinedPrompt, inputData);

            // Process model outputs (for Arena experiments with A/B comparisons)
            var modelResults = new List<object>();
            for (int i = 0; i < outputData.Count; i++)
            {
                var output = outputData[i];
                var outputContent = new List<object>();
                
                foreach (var content in output.Output)
                {
                    if (content.Type.StartsWith("imageurl"))
                    {
                        try
                        {
                            var (base64Image, mimeType) = await GetBase64ImageWithType(content);
                            outputContent.Add(new
                            {
                                type = "Image",
                                data = $"data:{mimeType};base64,{base64Image}",
                                location = (string?)null,
                                metadata = (object?)null,
                                highlighted_segments = new object[] { }
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Error processing output image");
                            outputContent.Add(new
                            {
                                type = "Text",
                                data = "Error loading image",
                                location = (string?)null,
                                metadata = (object?)null,
                                highlighted_segments = new object[] { }
                            });
                        }
                    }
                    else
                    {
                        outputContent.Add(new
                        {
                            type = "Text",
                            data = content.Content,
                            location = (string?)null,
                            metadata = (object?)null,
                            highlighted_segments = new object[] { }
                        });
                    }
                }

                modelResults.Add(new
                {
                    input_id = inputId,
                    completions = new
                    {
                        content = outputContent
                    },
                    finish_reason = "stop",
                    error = (string?)null,
                    metadata = new
                    {
                        model_name = output.ModelId,
                    }
                });
            }

            // Create Python-compatible ModelRun structure
            return new
            {
                id = ModelId,
                model = new
                {
                    name = ModelId,
                    version = "1.0"
                },
                dataset = new
                {
                    name = string.IsNullOrEmpty(clinicalTaskId) ? "arena_evaluation" : $"clinical_task_{clinicalTaskId}",
                    description = string.IsNullOrEmpty(clinicalTaskId) ? "Arena experiment evaluation dataset" : $"Dataset for clinical task {clinicalTaskId}",
                    instances = new[]
                    {
                        new
                        {
                            id = inputId,
                            input = new
                            {
                                content = inputContent
                            },
                            references = new object[] { },
                            split = "Test",
                            sub_split = (string?)null,
                            perturbation = (string?)null,
                        }
                    }
                },
                results = modelResults
            };
        }

        private string GenerateInputId(string prompt, List<DataContent> inputData)
        {
            var contentBuilder = new StringBuilder();
            contentBuilder.Append(prompt);
            
            foreach (var input in inputData)
            {
                contentBuilder.Append("|");
                contentBuilder.Append(input.Type);
                contentBuilder.Append(":");
                contentBuilder.Append(input.Content);
            }
            
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(contentBuilder.ToString()));
            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }


        private string GetParameter(string key)
        {
            if (_settings.TryGetValue(key, out string? value))
            {
                return value;
            }
            throw new ArgumentException($"Required parameter '{key}' not found in settings");
        }

        private string GetStorageConnectionString()
        {
            // First try to get from model settings
            if (_settings.TryGetValue(FunctionAppRunnerSettings.StorageConnectionString, out string? settingsConnectionString) 
                && !string.IsNullOrEmpty(settingsConnectionString))
            {
                return settingsConnectionString;
            }

            // Fall back to configuration
            using var scope = _scopeFactory.CreateScope();
            var configuration = scope.ServiceProvider.GetService<IConfiguration>();
            if (configuration != null)
            {
                var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") 
                       ?? configuration["AzureStorage:ConnectionString"];
                if (!string.IsNullOrEmpty(connectionString))
                {
                    return connectionString;
                }
            }

            throw new InvalidOperationException("No storage connection string found in model settings or environment variables.");
        }


        public override void Dispose()
        {
            base.Dispose();
            // BlobServiceClient doesn't need explicit disposal
        }
    }
}
