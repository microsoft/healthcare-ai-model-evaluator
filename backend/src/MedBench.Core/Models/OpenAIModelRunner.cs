using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Azure;
using Azure.AI.OpenAI;
using MedBench.Core.Interfaces;
using OpenAI.Chat;
using ZstdSharp.Unsafe;

namespace MedBench.Core.Models
{
    
    public class OpenAIModelRunner : ModelRunnerBase, IModelRunner
    {
        const int MAX_IMAGE_SIZE = 65519;
        private readonly AzureOpenAIClient _client;
        private readonly string _deploymentName;

        public OpenAIModelRunner(
            Dictionary<string, string> settings,
            IImageService imageService,
            IServiceScopeFactory scopeFactory, 
            ILogger logger,
            string modelId) : base(settings, imageService, scopeFactory, logger, modelId)
        {
            _client = new AzureOpenAIClient(
                new Uri(settings["ENDPOINT"]),
                new AzureKeyCredential(settings["API_KEY"]));
            _deploymentName = settings["DEPLOYMENT"];
        }

        public override async Task<string> GenerateOutput(string prompt, List<DataContent> inputData, List<ModelOutput> outputData)
        {
            Console.WriteLine("Generating output");
            if (_isDisposed) throw new ObjectDisposedException(nameof(OpenAIModelRunner));

            ChatClient chatClient = _client.GetChatClient(_deploymentName);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt),
                new SystemChatMessage("Input Data:"),
            };

            foreach (var input in inputData)
            {
                try 
                {
                    if (input.Type.StartsWith("imageurl"))
                    {
                        var (base64Image, mimeType) = await GetBase64ImageWithType(input);
                        Console.WriteLine($"Processing image of size: {base64Image.Length}");
                        
                        var imageBytes = BinaryData.FromBytes(Convert.FromBase64String(base64Image));
                        messages.Add(new UserChatMessage(new ChatMessageContent(
                            new[] { ChatMessageContentPart.CreateImagePart(imageBytes, mimeType) }
                        )));
                    }
                    else
                    {
                        messages.Add(new UserChatMessage(input.Content));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing input: {ex.Message}");
                    throw;
                }
            }
            var modelCount = 0;
            foreach (var outputModel in outputData)
            {
                modelCount++;
                messages.Add(new SystemChatMessage("Output for model" + ((outputData.Count() > 1) ? (modelCount==1 ? " A" : " B") : "") + ":"));
                foreach (var output in outputModel.Output)
                {
                    try 
                    {
                        if (output.Type.StartsWith("imageurl"))
                        {
                            var (base64Image, mimeType) = await GetBase64ImageWithType(output);
                            Console.WriteLine($"Processing image of size: {base64Image.Length}");
                            
                            var imageBytes = BinaryData.FromBytes(Convert.FromBase64String(base64Image));
                            messages.Add(new UserChatMessage(new ChatMessageContent(
                                new[] { ChatMessageContentPart.CreateImagePart(imageBytes, mimeType) }
                            )));
                        }
                        else
                        {
                            messages.Add(new UserChatMessage(output.Content));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing input: {ex.Message}");
                        throw;
                    }
                }
            }
            var response = await chatClient.CompleteChatAsync(messages);
            var results = ""; 
            foreach (var content in response.Value.Content)
            {
                results += content.Text;
            }
            Console.WriteLine("Generated output");
            return results;
            
        }
    }
} 