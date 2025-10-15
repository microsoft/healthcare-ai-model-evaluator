using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Models
{

    public class OpenAIReasoningModelRunner : ModelRunnerBase, IModelRunner
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _deployment;
        private readonly string _apiVersion;
        private readonly int _maxTokens;
        private readonly string _reasoningEffort;

        public OpenAIReasoningModelRunner(
            Dictionary<string, string> settings,
            IImageService imageService,
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            string modelId) : base(settings, imageService, scopeFactory, logger, modelId)
        {
            _httpClient = new HttpClient();
            _endpoint = settings["ENDPOINT"];
            _apiKey = settings["API_KEY"];
            _deployment = settings["DEPLOYMENT"];
            _apiVersion = settings.GetValueOrDefault("API_VERSION", "2025-01-01-preview");
            _maxTokens = int.Parse(settings.GetValueOrDefault("MAX_TOKENS", "6000"));
            _reasoningEffort = settings.GetValueOrDefault("REASONING_EFFORT", "low"); // Default from API specs is "medium"
            
            // Set up HTTP client headers
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public override async Task<string> GenerateOutput(string prompt, List<DataContent> inputData, List<ModelOutput> outputData)
        {
            Console.WriteLine("Generating reasoning output...");
            if (_isDisposed) throw new ObjectDisposedException(nameof(OpenAIReasoningModelRunner));

            try
            {
                var messages = await BuildMessages(prompt, inputData, outputData);
                var requestBody = BuildRequestBody(messages);
                var response = await SendHttpRequest(requestBody);
                
                return ParseResponse(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OpenAIReasoningModelRunner: {ex.Message}");
                throw;
            }
        }

        private async Task<List<object>> BuildMessages(string prompt, List<DataContent> inputData, List<ModelOutput> outputData)
        {
            var messages = new List<object>
            {
                new { role = "system", content = prompt },
                new { role = "system", content = "Input Data:" }
            };

            // Process input data
            foreach (var input in inputData)
            {
                if (input.Type.StartsWith("imageurl"))
                {
                    var (base64Image, mimeType) = await GetBase64ImageWithType(input);
                    Console.WriteLine($"Processing image of size: {base64Image.Length}");
                    
                    messages.Add(new
                    {
                        role = "user",
                        content = new[]
                        {
                            new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                        }
                    });
                }
                else
                {
                    messages.Add(new { role = "user", content = input.Content });
                }
            }

            // Process output data for A/B testing scenarios
            var modelCount = 0;
            foreach (var outputModel in outputData)
            {
                modelCount++;
                var modelLabel = outputData.Count > 1 ? (modelCount == 1 ? " A" : " B") : "";
                messages.Add(new { role = "system", content = $"Output for model{modelLabel}:" });
                
                foreach (var output in outputModel.Output)
                {
                    if (output.Type.StartsWith("imageurl"))
                    {
                        var (base64Image, mimeType) = await GetBase64ImageWithType(output);
                        messages.Add(new
                        {
                            role = "user",
                            content = new[]
                            {
                                new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                            }
                        });
                    }
                    else
                    {
                        messages.Add(new { role = "user", content = output.Content });
                    }
                }
            }

            return messages;
        }

        private object BuildRequestBody(List<object> messages)
        {
            var requestBody = new Dictionary<string, object>
            {
                ["messages"] = messages,
                ["max_completion_tokens"] = _maxTokens,
            };

            // Add reasoning-specific parameters if available
            if (!string.IsNullOrEmpty(_reasoningEffort))
            {
                requestBody["reasoning_effort"] = _reasoningEffort;
            }

            Console.WriteLine($"Request parameters: {string.Join(", ", requestBody.Keys)}");
            return requestBody;
        }

        private async Task<string> SendHttpRequest(object requestBody)
        {
            var url = $"{_endpoint}openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";
            
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false 
            });
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            Console.WriteLine($"Making request to: {url} with body: {json}");
            
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP {response.StatusCode}: {responseContent}");
                throw new HttpRequestException($"HTTP {response.StatusCode}: {responseContent}");
            }
            
            Console.WriteLine($"HTTP {response.StatusCode}: Response received");
            return responseContent;
        }

        private string ParseResponse(string responseContent)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(responseContent);
                var choices = jsonDoc.RootElement.GetProperty("choices");
                
                if (choices.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("No choices returned in response");
                }
                
                var firstChoice = choices[0];
                var message = firstChoice.GetProperty("message");
                var content = message.GetProperty("content").GetString();
                
                // Log reasoning tokens if available (for o3-mini)
                if (jsonDoc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("completion_tokens_details", out var details))
                    {
                        if (details.TryGetProperty("reasoning_tokens", out var reasoningTokens))
                        {
                            Console.WriteLine($"Reasoning tokens used: {reasoningTokens.GetInt32()}");
                        }
                    }
                    
                    // Log total token usage
                    if (usage.TryGetProperty("total_tokens", out var totalTokens))
                    {
                        Console.WriteLine($"Total tokens used: {totalTokens.GetInt32()}");
                    }
                }
                
                return content ?? "Empty response content";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing response: {ex.Message}");
                Console.WriteLine($"Raw response: {responseContent}");
                throw new InvalidOperationException($"Failed to parse response: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            _httpClient?.Dispose();
            base.Dispose();
        }
    }
}