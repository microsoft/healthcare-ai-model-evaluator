using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MedBench.Core.Models;
using MedBench.Core.Services;
using MedBench.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

public class CXRReportGenModelRunner : ModelRunnerBase, IModelRunner
{
    private readonly HttpClient _client;

    public CXRReportGenModelRunner(
        Dictionary<string, string> settings,
        IImageService imageService,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        string modelId) : base(settings, imageService, scopeFactory, logger, modelId)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings["API_KEY"]}");
        _client.DefaultRequestHeaders.Add(
            "azureml-model-deployment", 
            $"{settings["DEPLOYMENT"]}-{settings["VERSION"]}");
    }

    public override async Task<string> GenerateOutput(string prompt, List<DataContent> inputData, List<ModelOutput> outputData)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(CXRReportGenModelRunner));

        var images = new List<string>();
        var texts = new List<string>();

        // Process input data
        foreach (var input in inputData)
        {
            if (input.Type.StartsWith("imageurl"))
            {
                var (base64Image, mimeType) = await GetBase64ImageWithType(input);
                images.Add(base64Image);
            }
            if (input.Type.StartsWith("imagedata"))
            {
                images.Add(input.Content);
            }
            else if (input.Type.StartsWith("text"))
            {
                texts.Add(input.Content);
            }
        }

        // Process model outputs if any
        var modelCount = 0;
        foreach (var outputModel in outputData)
        {
            modelCount++;
            texts.Add($"Output for model{(outputData.Count() > 1 ? (modelCount == 1 ? " A" : " B") : "")}:");
            foreach (var modelOutput in outputModel.Output)
            {
                if (modelOutput.Type.StartsWith("imageurl"))
                {
                    var (base64Image, mimeType) = await GetBase64ImageWithType(modelOutput);
                    images.Add(base64Image);
                }
                else
                {
                    texts.Add(modelOutput.Content);
                }
            }
        }

        if (!images.Any())
            return "no images provided";

        Console.WriteLine($"Available settings keys: {string.Join(", ", _settings.Keys)}");
        Console.WriteLine($"ENDPOINT value: '{_settings.GetValueOrDefault("ENDPOINT", "NOT_FOUND")}'");

        var payload = new
        {
            input_data = new
            {
                data = new[] { new[] { images[0], texts.FirstOrDefault() ?? "" } },
                columns = new[] { "frontal_image", "indication" },
                index = new[] { 0 }
            }
        };

        var endpoint = _settings.GetValueOrDefault("ENDPOINT", "");
        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException($"ENDPOINT setting is missing or empty. Available settings: {string.Join(", ", _settings.Keys)}");
        }

        var response = await _client.PostAsJsonAsync(endpoint, payload);
        Console.WriteLine("response: " + response);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<Dictionary<string, string>>>() ?? throw new InvalidOperationException("No response data received");
        string output = "";
        foreach (var dict in result)
        {
            foreach (var kvp in dict)
            {
                Console.WriteLine($"  Key: {kvp.Key}, Value: {kvp.Value}");
                if (kvp.Key == "output")
                {
                    output = kvp.Value;
                }
            }
        }

        return output;
    }

    public override void Dispose()
    {
        base.Dispose();
        _client.Dispose();
    }
} 