using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MedBench.Core.Models
{

    public abstract class ModelRunnerBase : IModelRunner
    {
        protected readonly ILogger _logger;
        protected readonly Dictionary<string, string> _settings;
        protected readonly IImageService _imageService;
        protected readonly IServiceScopeFactory _scopeFactory;
        protected bool _isDisposed;
        public string ModelId { get; set; }
        protected ModelRunnerBase(
            Dictionary<string, string> settings,
            IImageService imageService,
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            string modelId)
        {
            _settings = settings;
            _imageService = imageService;
            _scopeFactory = scopeFactory;
            _logger = logger;
            ModelId = modelId;
        }

        protected async Task<(string base64, string mimeType)> GetBase64ImageWithType(DataContent content)
        {
            using var scope = _scopeFactory.CreateScope();
            var imageRepo = scope.ServiceProvider.GetRequiredService<IImageRepository>();
            var image = await imageRepo.GetByIdAsync(content.Content);
            var stream = await _imageService.GetImageStreamAsync(image);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return (
                Convert.ToBase64String(memoryStream.ToArray()),
                image.ContentType // e.g. "image/jpeg", "image/png"
            );
        }

        public abstract Task<string> GenerateOutput(string prompt, List<DataContent> inputData, List<ModelOutput> outputData);

        public virtual Task<string> GenerateOutput(string basePrompt, string outputInstructions, List<DataContent> inputData, List<ModelOutput> outputData)
        {
            // Default implementation: concatenate base prompt with output instructions
            var combinedPrompt = CombineBasePromptAndInstructions(basePrompt, outputInstructions);
            return GenerateOutput(combinedPrompt, inputData, outputData);
        }

        /// <summary>
        /// Combines base prompt and output instructions into a single prompt string.
        /// </summary>
        /// <param name="basePrompt">The base prompt text</param>
        /// <param name="outputInstructions">The output instructions text</param>
        /// <returns>Combined prompt string</returns>
        protected virtual string CombineBasePromptAndInstructions(string basePrompt, string outputInstructions)
        {
            return string.IsNullOrEmpty(outputInstructions) 
                ? basePrompt 
                : $"{basePrompt}\n{outputInstructions}";
        }

        public virtual Task<List<DataContent>> ProcessInputDataForModel(List<DataContent> inputData)
        {
            return Task.FromResult(inputData);
        }

        public virtual void Dispose()
        {
            _isDisposed = true;
        }
    }
} 