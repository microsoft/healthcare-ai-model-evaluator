using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IModelRunner : IDisposable
{
    string ModelId { get; set; }
    Task<string> GenerateOutput(string prompt, List<DataContent> inputData, List<ModelOutput> outputData);
    Task<string> GenerateOutput(string basePrompt, string outputInstructions, List<DataContent> inputData, List<ModelOutput> outputData);
    Task<List<DataContent>> ProcessInputDataForModel(List<DataContent> inputData);
} 