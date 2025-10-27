using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Azure;
using Azure.AI.Inference;
using MedBench.Core.Interfaces;
using OpenAI.Chat;
using ZstdSharp.Unsafe;
using System.Text.Json;  // Add at the top
using System.Text.Json.Serialization;
using System.Threading;

namespace MedBench.Core.Models
{
    public class AzureServelessEndpointRunner : ModelRunnerBase, IModelRunner
    {
        private readonly ChatCompletionsClient _client;

        public AzureServelessEndpointRunner(
            Dictionary<string, string> settings,
            IImageService imageService,
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            string modelId) : base(settings, imageService, scopeFactory, logger, modelId)
        {
            _client = new ChatCompletionsClient(
                new Uri(settings["ENDPOINT"]),
                new AzureKeyCredential(settings["API_KEY"])
                // Some endpoints support Entra ID authentication
                //new DefaultAzureCredential(includeInteractiveCredentials: true)
            );
        }

        public override async Task<string> GenerateOutput(string prompt, List<DataContent> inputData, List<ModelOutput> outputData)
        {
            Console.WriteLine("Generating output");
            if (_isDisposed) throw new ObjectDisposedException(nameof(AzureServelessEndpointRunner));

            var requestOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatRequestSystemMessage(prompt),
                },
            };
            var userMessageContent = new List<ChatMessageContentItem> { };

            foreach (var input in inputData)
            {
                if (input.Type.StartsWith("imageurl"))
                {
                    var (base64Image, mimeType) = await GetBase64ImageWithType(input);
                    userMessageContent.Add(
                        new ChatMessageImageContentItem(
                            BinaryData.FromBytes(Convert.FromBase64String(base64Image)),
                            mimeType
                        )
                    );
                }
                else
                {
                    Console.WriteLine("Adding text content: "+input.Content);
                    userMessageContent.Add(new ChatMessageTextContentItem(input.Content));
                }
            }
            var modelCount = 0;
            foreach (var outputModel in outputData)
            {
                modelCount++;
                userMessageContent.Add(new ChatMessageTextContentItem($"Output for model{(outputData.Count() > 1 ? (modelCount == 1 ? " A" : " B") : "")}:"));
                foreach (var modelOutput in outputModel.Output)
                {
                    if (modelOutput.Type.StartsWith("imageurl"))
                    {
                        var (base64Image, mimeType) = await GetBase64ImageWithType(modelOutput);
                        userMessageContent.Add(
                            new ChatMessageImageContentItem(
                                BinaryData.FromBytes(Convert.FromBase64String(base64Image)),
                                mimeType
                            )
                        );
                    }
                    else
                    {
                        Console.WriteLine("Adding text content: "+modelOutput.Content);
                        userMessageContent.Add(new ChatMessageTextContentItem(modelOutput.Content));
                    }
                }
            }
            requestOptions.Messages.Add(new ChatRequestUserMessage(userMessageContent));
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            Console.WriteLine("Sending request to Azure: " + 
                JsonSerializer.Serialize(requestOptions.Messages, options));
            foreach (var message in requestOptions.Messages)
            {
                if (message == null) continue;
                
                if (message is ChatRequestSystemMessage systemMessage)
                {
                    Console.WriteLine($"System Message: {systemMessage.Content ?? "null"}");
                }
                else if (message is ChatRequestUserMessage userMessage)
                {
                    Console.WriteLine("User Message:"+userMessage.MultimodalContentItems);
                    if (userMessage.Content != null || userMessage.MultimodalContentItems != null)
                    {
                        if(userMessage.Content != null){
                            foreach (var content in userMessage.Content)
                            {
                                Console.WriteLine($"  Content: {content}");
                            }
                        }
                        if(userMessage.MultimodalContentItems != null){
                            foreach (var content in userMessage.MultimodalContentItems)
                            {
                                Console.WriteLine($"  Content: {content}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("  Content is null");
                    }
                }
            }

            // Add retry logic for the API call
            int maxRetries = 3;
            int currentRetry = 0;
            TimeSpan timeout = TimeSpan.FromSeconds(30);
            
            while (currentRetry < maxRetries)
            {
                try
                {
                    using var cts = new CancellationTokenSource(timeout);
                    Response<ChatCompletions> response = await _client.CompleteAsync(
                        requestOptions, 
                        cts.Token
                    );
                    var output = response.Value.Content;
                    Console.WriteLine("Generated output: "+output);
                    return output;
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    currentRetry++;
                    if (currentRetry >= maxRetries)
                    {
                        throw new TimeoutException($"Operation timed out after {maxRetries} attempts of {timeout.TotalSeconds} seconds each", ex);
                    }
                    Console.WriteLine($"Request timed out, attempt {currentRetry} of {maxRetries}");
                    await Task.Delay(1000); // Wait 1 second between retries
                }
            }
            
            throw new Exception("Failed to generate output after maximum retries");
        }
    }
}