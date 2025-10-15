using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpToken;

namespace MedBench.Core.Services;

public class DataFileService : IDataFileService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<DataFileService> _logger;
    private readonly IDataObjectRepository _dataObjectRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly IImageService _imageService;
    private readonly GptEncoding _encoding;

    public DataFileService(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<DataFileService> logger,
        IDataObjectRepository dataObjectRepository,
        IDataSetRepository dataSetRepository,
        IImageService imageService)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["AzureStorage:DataFilesContainer"] ?? "datafiles";
        _logger = logger;
        _dataSetRepository = dataSetRepository;
        _dataObjectRepository = dataObjectRepository;
        _imageService = imageService;
        _encoding = GptEncoding.GetEncoding("cl100k_base");
    }

    private string ParseBlobName(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        string path = uri.AbsolutePath.TrimStart('/');
        
        // Find the container name in the path and extract everything after it
        string containerPattern = $"{_containerName}/";
        int containerIndex = path.IndexOf(containerPattern);
        
        if (containerIndex == -1)
        {
            throw new ArgumentException($"Container name '{_containerName}' not found in blob URL: {blobUrl}");
        }
        
        // Extract the blob path after the container name
        int blobStartIndex = containerIndex + containerPattern.Length;
        if (blobStartIndex >= path.Length)
        {
            throw new ArgumentException($"No blob path found after container name in URL: {blobUrl}");
        }
        
        return path.Substring(blobStartIndex);
    }

    public async Task<string> UploadDataFileAsync(IFormFile file, string datasetId)
    {
        try
        {
            _logger.LogInformation("Connecting to container {_containerName}", _containerName);
            // Get container client - matching ImageService pattern
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();
            
            // Generate a unique blob name
            string blobName = $"{datasetId}/{Guid.NewGuid()}-{file.FileName}";
            var blobClient = containerClient.GetBlobClient(blobName);
            
            // Upload the file
            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
            
            // Return the blob URL
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading data file {FileName}", file.FileName);
            throw;
        }
    }

    public async Task<Stream> GetDataFileStreamAsync(string blobUrl)
    {
        try
        {
            // Parse the blob URL to get blob name
            var blobName = ParseBlobName(blobUrl);
            
            _logger.LogInformation("Connecting to container {_containerName}", _containerName);
            // Get container client - matching ImageService pattern
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();
            var blobClient = containerClient.GetBlobClient(blobName);
            
            // Download the blob
            var response = await blobClient.DownloadAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data file stream for {BlobUrl}", blobUrl);
            throw;
        }
    }
    
    public async Task DeleteDataFileAsync(string blobUrl)
    {
        try
        {
            // Parse the blob URL to get blob name
            var blobName = ParseBlobName(blobUrl);
            
            _logger.LogInformation("Connecting to container {_containerName}", _containerName);
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data file");
            throw;
        }
    }
    
    public async Task ProcessDataFileAsync(DataSet dataset, DataFile dataFile)
    {
        // Update the processing status to Processing
        dataFile.ProcessingStatus = DataFileProcessingStatus.Processing;
        await _dataSetRepository.UpdateAsync(dataset);
        
        try
        {
            var blobStream = await GetDataFileStreamAsync(dataFile.BlobUrl);
            var dataObjects = new List<DataObject>();
            var totalTokens = 0;
            var totalInputTokens = 0;
            var totalOutputTokens = 0;
            var totalOutputTokensPerIndex = new Dictionary<string, int>();
            var lineCount = 0;
            if( dataset.TotalOutputTokensPerIndex != null)
            {
                totalOutputTokensPerIndex = dataset.TotalOutputTokensPerIndex;
            }
            
            using (var reader = new StreamReader(blobStream))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    lineCount++;
                    try
                    {
                        // Add NaN handling
                        if (line.Contains("NaN"))
                        {
                            line = System.Text.RegularExpressions.Regex.Replace(
                                line, 
                                @":\s*NaN\s*([,}])", 
                                @":""NaN""$1"
                            );
                        }
                        var jsonObject = JsonSerializer.Deserialize<JsonElement>(line);
                        var dataObject = await CreateDataObjectFromJson(jsonObject, dataFile.Mapping, dataset.Id);
                        if( dataObject == null)
                        {
                            continue;
                        }
                        dataObjects.Add(dataObject);
                        totalTokens += dataObject.TotalTokens;
                        totalInputTokens += dataObject.TotalInputTokens;
                        totalOutputTokens += dataObject.TotalOutputTokens;
                        if( dataObject.OutputData.Count > 0)
                        {
                            var count = 0;
                            foreach( var outputData in dataObject.OutputData)
                            {
                                if( totalOutputTokensPerIndex.ContainsKey(count.ToString()))
                                {
                                    totalOutputTokensPerIndex[count.ToString()] += outputData.TotalTokens;
                                }
                                else
                                {
                                    totalOutputTokensPerIndex[count.ToString()] = outputData.TotalTokens;
                                }
                                count++;
                            }
                        }
                        
                        // Process in batches to avoid memory issues
                        if (dataObjects.Count >= 100)
                        {
                            await _dataObjectRepository.CreateManyAsync(dataObjects);
                            dataFile.ProcessedObjectCount += dataObjects.Count;
                            dataset.DataObjectCount += dataObjects.Count;
                            dataset.TotalTokens += totalTokens;
                            dataset.TotalInputTokens += totalInputTokens;
                            dataset.TotalOutputTokens += totalOutputTokens;
                            dataset.TotalOutputTokensPerIndex = totalOutputTokensPerIndex;
                            await _dataSetRepository.UpdateAsync(dataset);
                            
                            dataObjects.Clear();
                            totalTokens = 0;
                            totalInputTokens = 0;
                            totalOutputTokens = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing line {LineNumber} in file {FileName}", lineCount, dataFile.FileName);
                    }
                }
                
                // Process any remaining data objects
                if (dataObjects.Any())
                {
                    await _dataObjectRepository.CreateManyAsync(dataObjects);
                    dataFile.ProcessedObjectCount += dataObjects.Count;
                    dataset.DataObjectCount += dataObjects.Count;
                    dataset.TotalTokens += totalTokens;
                    dataset.TotalInputTokens += totalInputTokens;
                    dataset.TotalOutputTokens += totalOutputTokens;
                    dataset.TotalOutputTokensPerIndex = totalOutputTokensPerIndex;
                }
            }
            
            // Update the status to completed
            dataFile.ProcessingStatus = DataFileProcessingStatus.Completed;
            dataFile.TotalObjectCount = dataFile.ProcessedObjectCount;
            dataset.ModelOutputCount = dataFile.Mapping.OutputMappings.Count;
            await _dataSetRepository.UpdateAsync(dataset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data file {FileName}", dataFile.FileName);
            dataFile.ProcessingStatus = DataFileProcessingStatus.Failed;
            dataFile.ErrorMessage = ex.Message;
            await _dataSetRepository.UpdateAsync(dataset);
            throw;
        }
    }
    
    public async Task<DataObject?> CreateDataObjectFromJson(JsonElement jsonObject, DataFileMapping mapping, string datasetId)
    {
        Console.WriteLine(jsonObject.GetRawText());
        Console.WriteLine(mapping.InputMappings);

        var dataObject = new DataObject
        {
            DataSetId = datasetId,
            Name = string.Empty,
            Description = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            InputData = new List<DataContent>(),
            OutputData = new List<DataContent>(),
            GeneratedOutputData = new List<DataContent>()
        };
        
        int objectTotalTokens = 0;
        int objectTotalInputTokens = 0;
        int objectTotalOutputTokens = 0;
        
        // Process input mappings
        foreach (var inputMapping in mapping.InputMappings)
        {
            if (inputMapping.IsArray)
            {
                // Handle array mapping
                JsonElement arrayElement;
                string lastKey = "";
                
                if (inputMapping.KeyPath.Count == 1)
                {
                    // Direct array processing: ["inputs"] -> process the array at "inputs"
                    arrayElement = GetJsonElementFromPath(jsonObject, inputMapping.KeyPath);
                    lastKey = ""; // No last key to extract, use array elements directly
                }
                else
                {
                    // Array element property processing: ["images", "url"] -> get array at "images" and extract "url"
                    var arrayPath = inputMapping.KeyPath.Take(inputMapping.KeyPath.Count - 1).ToList();
                    arrayElement = GetJsonElementFromPath(jsonObject, arrayPath);
                    lastKey = inputMapping.KeyPath.Last();
                }
                
                if (arrayElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var arrayItem in arrayElement.EnumerateArray())
                    {
                        string value = "";
                        
                        if (string.IsNullOrEmpty(lastKey))
                        {
                            // Use the array item directly
                            value = GetValueFromElement(arrayItem);
                        }
                        else
                        {
                            // Extract the specific key from the array item
                            value = GetValueFromPath(arrayItem, new[] { lastKey }) ?? string.Empty;
                        }
                        
                        var dataContent = new DataContent
                        {
                            Type = inputMapping.Type,
                            Content = value
                        };
                        
                        // Handle image URLs
                        if (inputMapping.Type == "imageurl" && value.StartsWith("data:image"))
                        {
                            try
                            {
                                var image = await _imageService.ProcessDataUrlAndSaveImageAsync(value);
                                dataContent.Content = image.Id;
                                dataContent.TotalTokens = 0; // Images don't count as tokens
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing image URL");
                            }
                        }
                        else
                        {
                            dataContent.Content = dataContent.Content.Replace("\\n", "\n");
                            dataContent.TotalTokens = CountTokens(dataContent.Content);
                            objectTotalTokens += dataContent.TotalTokens;
                            objectTotalInputTokens += dataContent.TotalTokens;
                        }
                        
                        dataObject.InputData.Add(dataContent);
                    }
                }
            }
            else
            {
                // Handle regular (non-array) mapping
                string value = GetValueFromPath(jsonObject, inputMapping.KeyPath) ?? string.Empty;
                
                var dataContent = new DataContent
                {
                    Type = inputMapping.Type,
                    Content = value
                };
                
                // Handle image URLs
                if (inputMapping.Type == "imageurl" && value.StartsWith("data:image"))
                {
                    try
                    {
                        var image = await _imageService.ProcessDataUrlAndSaveImageAsync(value);
                        dataContent.Content = image.Id;
                        dataContent.TotalTokens = 0; // Images don't count as tokens
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing image URL");
                    }
                }
                else
                {
                    dataContent.Content = dataContent.Content.Replace("\\n", "\n");
                    dataContent.TotalTokens = CountTokens(dataContent.Content);
                    objectTotalTokens += dataContent.TotalTokens;
                    objectTotalInputTokens += dataContent.TotalTokens;
                }
                
                dataObject.InputData.Add(dataContent);
            }
        }

        // Process output mappings similarly
        foreach (var outputMapping in mapping.OutputMappings)
        {
            if (outputMapping.IsArray)
            {
                // Handle array mapping
                JsonElement arrayElement;
                string lastKey = "";
                
                if (outputMapping.KeyPath.Count == 1)
                {
                    // Direct array processing: ["outputs"] -> process the array at "outputs"
                    arrayElement = GetJsonElementFromPath(jsonObject, outputMapping.KeyPath);
                    lastKey = ""; // No last key to extract, use array elements directly
                }
                else
                {
                    // Array element property processing: ["images", "caption"] -> get array at "images" and extract "caption"
                    var arrayPath = outputMapping.KeyPath.Take(outputMapping.KeyPath.Count - 1).ToList();
                    arrayElement = GetJsonElementFromPath(jsonObject, arrayPath);
                    lastKey = outputMapping.KeyPath.Last();
                }
                
                if (arrayElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var arrayItem in arrayElement.EnumerateArray())
                    {
                        string value = "";
                        
                        if (string.IsNullOrEmpty(lastKey))
                        {
                            // Use the array item directly
                            value = GetValueFromElement(arrayItem);
                        }
                        else
                        {
                            // Extract the specific key from the array item
                            value = GetValueFromPath(arrayItem, new[] { lastKey }) ?? string.Empty;
                        }
                        
                        var dataContent = new DataContent
                        {
                            Type = outputMapping.Type,
                            Content = value,
                            TotalTokens = CountTokens(value)
                        };
                        
                        objectTotalTokens += dataContent.TotalTokens;
                        objectTotalOutputTokens += dataContent.TotalTokens;
                        dataObject.OutputData.Add(dataContent);
                    }
                }
            }
            else
            {
                // Handle regular (non-array) mapping
                string value = GetValueFromPath(jsonObject, outputMapping.KeyPath) ?? string.Empty;
                
                var dataContent = new DataContent
                {
                    Type = outputMapping.Type,
                    Content = value,
                    TotalTokens = CountTokens(value)
                };
                
                objectTotalTokens += dataContent.TotalTokens;
                objectTotalOutputTokens += dataContent.TotalTokens;
                dataObject.OutputData.Add(dataContent);
            }
        }
        
        dataObject.TotalTokens = objectTotalTokens;
        dataObject.TotalInputTokens = objectTotalInputTokens;
        dataObject.TotalOutputTokens = objectTotalOutputTokens;
        //If all the input data is empty return null
        if( dataObject.InputData.All(x => string.IsNullOrEmpty(x.Content)))
        {
            return null;
        }
        //If all the output data is empty return null
        if(dataObject.InputData.All(x => string.IsNullOrEmpty(x.Content)) && dataObject.OutputData.All(x => string.IsNullOrEmpty(x.Content)))
        {
            return null;
        }
        return dataObject;
    }
    
    private JsonElement GetJsonElementFromPath(JsonElement element, IEnumerable<string> path)
    {
        JsonElement current = element;
        
        foreach (var segment in path)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var property))
            {
                current = property;
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index) && index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else
            {
                // Return a default JsonElement if path doesn't exist
                return new JsonElement();
            }
        }
        
        return current;
    }
    
    private string GetValueFromElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;
            case JsonValueKind.Number:
                return element.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return string.Empty;
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                return element.GetRawText();
            default:
                return string.Empty;
        }
    }
    
    private string GetValueFromPath(JsonElement element, IEnumerable<string> path)
    {
        JsonElement current = element;
        
        foreach (var segment in path)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var property))
            {
                current = property;
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index) && index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else
            {
                return string.Empty;
            }
        }
        
        switch (current.ValueKind)
        {
            case JsonValueKind.String:
                return current.GetString() ?? string.Empty;
            case JsonValueKind.Number:
                return current.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return string.Empty;
            case JsonValueKind.Object:
            case JsonValueKind.Array:
                return current.GetRawText();
            default:
                return string.Empty;
        }
    }
    
    private int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        try
        {
            // Remove or replace the problematic token if present
            text = text.Replace("<|endoftext|>", "");
            
            // Get token count using proper tokenization
            return _encoding.Encode(text).Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting tokens");
            // Fallback to simple approximation if tokenization fails
            return text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?' }, 
                StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
} 