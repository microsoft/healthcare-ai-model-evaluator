using Microsoft.AspNetCore.Mvc;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using Microsoft.Extensions.Logging;
using SharpToken;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using MedBench.Core.Services;

namespace MedBench.API.Controllers;

public class DataSetListDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AiModelType { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public int DataObjectCount { get; set; }
    public int ModelOutputCount { get; set; }
    public int TotalTokens { get; set; }
    public List<string> GeneratedDataList { get; set; } = new List<string>();
    public List<DataFile> Files { get; set; } = new List<DataFile>();

    public Dictionary<string, int> TotalOutputTokensPerIndex { get; set; } = new Dictionary<string, int>();
    public int TotalOutputTokens { get; set; }

    public int TotalInputTokens { get; set; }
    
    // Data retention
    public int DaysToAutoDelete { get; set; } = 180;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class DataSetDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReviewerInstructions { get; set; } = string.Empty;
    public string AiModelType { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public int DataObjectCount { get; set; }
    public int ModelOutputCount { get; set; }
    public List<string> GeneratedDataList { get; set; } = new List<string>();
    public List<DataFile> Files { get; set; } = new List<DataFile>();
    
    // Data retention
    public int DaysToAutoDelete { get; set; } = 180;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class CreateDataSetDto
{
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReviewerInstructions { get; set; } = string.Empty;
    public string AiModelType { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public List<DataObject> DataObjects { get; set; } = new List<DataObject>();
    public int ModelOutputCount { get; set; }
    public int DaysToAutoDelete { get; set; } = 180; // Default 180 days

}
public class UpdateDataSetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReviewerInstructions { get; set; } = string.Empty;
    public string AiModelType { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public List<DataObject>? DataObjects { get; set; } = new List<DataObject>();
    public int ModelOutputCount { get; set; }
    public int DaysToAutoDelete { get; set; } = 180;
    
}

public class UploadDataFileRequest
{
    public IFormFile File { get; set; } = default!;
    public DataFileMappingDto Mapping { get; set; } = new DataFileMappingDto();
}

public class DataFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = "Queued";
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int ProcessedObjectCount { get; set; }
    public int TotalObjectCount { get; set; }
    public DataFileMappingDto Mapping { get; set; } = new DataFileMappingDto();
}

public class DataFileKeyPathDto
{
    public string Type { get; set; } = "text";
    public List<string> KeyPath { get; set; } = new List<string>();
    public bool IsArray { get; set; } = false;
}

public class DataFileMappingDto
{
    public List<DataFileKeyPathDto> InputMappings { get; set; } = new List<DataFileKeyPathDto>();
    public List<DataFileKeyPathDto> OutputMappings { get; set; } = new List<DataFileKeyPathDto>();
}

public class CreateDataSetWithFileDto
{
    // Dataset properties
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string? Description { get; set; } = string.Empty;
    public string AiModelType { get; set; } = string.Empty;
    public string? Tags { get; set; } = "[]";
    public int DaysToAutoDelete { get; set; } = 180;
    
    // File upload properties
    public IFormFile? File { get; set; }
    public string? Mapping { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DataSetsController : ControllerBase
{
    private readonly IDataSetRepository _repository;
    private readonly IDataObjectRepository _dataObjectRepository;
    private readonly ILogger<DataSetsController> _logger;
    private readonly IImageService _imageService;
    private readonly IDataFileService _dataFileService;

    public DataSetsController(IDataSetRepository repository, IDataObjectRepository dataObjectRepository, ILogger<DataSetsController> logger, IImageService imageService, IDataFileService dataFileService)
    {
        _repository = repository;
        _dataObjectRepository = dataObjectRepository;
        _logger = logger;
        _imageService = imageService;
        _dataFileService = dataFileService;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<DataSetListDto>>> GetAll()
    {
        var datasets = await _repository.GetAllAsync();
        var dtos = datasets.Select(ds => new DataSetListDto
        {
            Id = ds.Id,
            Name = ds.Name,
            Origin = ds.Origin,
            Description = ds.Description,
            AiModelType = ds.AiModelType,
            Tags = ds.Tags,
            DataObjectCount = ds.DataObjectCount,
            ModelOutputCount = ds.ModelOutputCount,
            TotalTokens = ds.TotalTokens,
            GeneratedDataList = ds.GeneratedDataList,
            Files = ds.DataFiles,
            TotalOutputTokensPerIndex = ds.TotalOutputTokensPerIndex,
            TotalOutputTokens = ds.TotalOutputTokens,
            TotalInputTokens = ds.TotalInputTokens,
            DaysToAutoDelete = ds.DaysToAutoDelete,
            CreatedAt = ds.CreatedAt,
            DeletedAt = ds.DeletedAt
        });
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<DataSetDetailDto>> GetById(string id)
    {
        try
        {
            var dataset = await _repository.GetByIdAsync(id);

            var detailDto = new DataSetDetailDto
            {
                Id = dataset.Id,
                Name = dataset.Name,
                Origin = dataset.Origin,
                Description = dataset.Description,
                AiModelType = dataset.AiModelType,
                Tags = dataset.Tags,
                DataObjectCount = dataset.DataObjectCount,
                ModelOutputCount = dataset.ModelOutputCount,
                GeneratedDataList = dataset.GeneratedDataList,
                Files = dataset.DataFiles,
                DaysToAutoDelete = dataset.DaysToAutoDelete,
                CreatedAt = dataset.CreatedAt,
                DeletedAt = dataset.DeletedAt
            };

            return Ok(detailDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        // Initialize GPT-3 encoder (cl100k_base is used by most recent OpenAI models)
        var encoding = GptEncoding.GetEncoding("cl100k_base");
        
        try
        {
            // Get token count using proper tokenization
            return encoding.Encode(text).Count;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error counting tokens: {ex.Message}");
            // Fallback to simple approximation if tokenization fails
            return text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?' }, 
                StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    private async Task ProcessDataObjectsAsync(IEnumerable<DataObject> dataObjects)
    {
        foreach (var dataObject in dataObjects)
        {
            int objectTotalInputTokens = 0;
            int objectTotalOutputTokens = 0;

            foreach (var inputData in dataObject.InputData)
            {
                if (inputData.Type?.ToLower() == "imageurl" && 
                    inputData.Content.StartsWith("data:image"))
                {
                    // Estimate tokens before processing the image
                    inputData.TotalTokens =  _dataFileService.EstimateImageTokens(inputData.Content);
                    objectTotalInputTokens += inputData.TotalTokens;
                    
                    var image = await _imageService.ProcessDataUrlAndSaveImageAsync(inputData.Content);
                    inputData.Content = image.Id;
                    inputData.ContentUrl = $"{image.StorageAccount}/{image.Container}/{image.BlobPath}";
                    inputData.Type = "imageurl";
                }
                else if (inputData.Type?.ToLower() == "text")
                {
                    inputData.Content = inputData.Content.Replace("\\n", "\n");
                    inputData.Type = "text";
                    inputData.TotalTokens = CountTokens(inputData.Content);
                    objectTotalInputTokens += inputData.TotalTokens;
                }
            }
            var totalOutputTokensPerIndex = new Dictionary<string, int>();
            var count = 0;
            foreach (var outputData in dataObject.OutputData)
            {
                if (outputData.Type?.ToLower() == "imageurl" && 
                    outputData.Content.StartsWith("data:image"))
                {
                    // Estimate tokens before processing the image
                    outputData.TotalTokens = _dataFileService.EstimateImageTokens(outputData.Content);
                    objectTotalOutputTokens += outputData.TotalTokens;
                    
                    var image = await _imageService.ProcessDataUrlAndSaveImageAsync(outputData.Content);
                    outputData.Content = image.Id;
                    outputData.ContentUrl = $"{image.StorageAccount}/{image.Container}/{image.BlobPath}";
                    outputData.Type = "imageurl";
                }
                else if (outputData.Type?.ToLower() == "text")
                {
                    outputData.Content = outputData.Content.Replace("\\n", "\n");
                    outputData.Type = "text";
                    outputData.TotalTokens = CountTokens(outputData.Content);
                    objectTotalOutputTokens += outputData.TotalTokens;
                }   
                totalOutputTokensPerIndex.Add(count.ToString(), outputData.TotalTokens);
                count++;
            }

            dataObject.TotalInputTokens = objectTotalInputTokens;
            dataObject.TotalOutputTokens = objectTotalOutputTokens;
            dataObject.TotalTokens = objectTotalInputTokens + objectTotalOutputTokens;
            dataObject.TotalOutputTokensPerIndex = totalOutputTokensPerIndex;
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DataSet>> Create([FromForm] CreateDataSetWithFileDto createDto)
    {
        try
        {
            _logger.LogInformation($"Received create request");
            
            var dataset = new DataSet
            {
                Name = createDto.Name,
                Origin = createDto.Origin,
                Description = createDto.Description ?? string.Empty,
                AiModelType = createDto.AiModelType,
                Tags = JsonSerializer.Deserialize<List<string>>(createDto.Tags ?? "[]") ?? new List<string>(),
                DataObjectCount = 0,
                ModelOutputCount = 0,
                TotalTokens = 0,
                GeneratedDataList = new List<string>(),
                DataFiles = new List<DataFile>(),
                DaysToAutoDelete = createDto.DaysToAutoDelete,
                CreatedAt = DateTime.UtcNow
            };
            
            // Create the dataset
            var createdDataset = await _repository.CreateAsync(dataset);
            _logger.LogInformation($"Dataset created: {createdDataset.Id}");
            _logger.LogInformation($"File: {createDto.File?.FileName}");
            _logger.LogInformation($"Raw Mapping: {createDto.Mapping}");
            DataFileMappingDto? mapping = null;
            try {
                var options = new JsonSerializerOptions { 
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };
                mapping = JsonSerializer.Deserialize<DataFileMappingDto>(createDto.Mapping ?? "{}", options);
                _logger.LogInformation($"Deserialized Mapping: {JsonSerializer.Serialize(mapping, options)}");
            } catch (JsonException ex) {
                _logger.LogError($"JSON Deserialization failed: {ex.Message}");
                return BadRequest($"Invalid mapping format: {ex.Message}");
            }
            _logger.LogInformation($"Mapping: {mapping?.InputMappings.Count}");
            // Handle file upload if provided
            if (createDto.File != null && 
                mapping != null && 
                mapping.InputMappings.Count > 0)
            {
                _logger.LogInformation($"Uploading file to Azure Blob Storage");
                // Upload the file to Azure Blob Storage
                var blobUrl = await _dataFileService.UploadDataFileAsync(createDto.File, createdDataset.Id);
                
                // Convert DTO mapping to domain mapping
                var dataFileMapping = new DataFileMapping
                {
                    InputMappings = mapping.InputMappings
                        .Select(m => new DataFileMappingItem { Type = m.Type, KeyPath = m.KeyPath, IsArray = m.IsArray })
                        .ToList(),
                    OutputMappings = mapping.OutputMappings
                        .Select(m => new DataFileMappingItem { Type = m.Type, KeyPath = m.KeyPath, IsArray = m.IsArray })
                        .ToList()
                };

                // Create a new DataFile entry
                var dataFile = new DataFile
                {
                    FileName = createDto.File.FileName,
                    BlobUrl = blobUrl,
                    ProcessingStatus = DataFileProcessingStatus.Unprocessed,
                    UploadedAt = DateTime.UtcNow,
                    Mapping = dataFileMapping
                };
                
                // Add the datafile to the dataset
                createdDataset.DataFiles.Add(dataFile);
                await _repository.UpdateAsync(createdDataset);
                
                // Start processing the file asynchronously
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await _dataFileService.ProcessDataFileAsync(createdDataset, dataFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing datafile {FileName}", createDto.File.FileName);
                    }
                });
            }
            
            return Ok(createdDataset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dataset");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<DataSet>> Update(string id, UpdateDataSetDto updateDto)
    {
        if (id != updateDto.Id)
            return BadRequest();

        try
        {
            // Process data objects first to calculate tokens
            if (updateDto.DataObjects?.Any() == true)
            {
                await ProcessDataObjectsAsync(updateDto.DataObjects);
            }

            var totalOutputTokensPerIndex = new Dictionary<string, int>();
            foreach (var dataObject in updateDto.DataObjects?.ToList() ?? new List<DataObject>())
            {
                var count = 0;
                foreach (var outputData in dataObject.OutputData)
                {
                    totalOutputTokensPerIndex.Add(count.ToString(), outputData.TotalTokens);
                    count++;
                }
                foreach (var generatedOutputData in dataObject.GeneratedOutputData)
                {
                    totalOutputTokensPerIndex.Add(generatedOutputData.GeneratedForClinicalTask, generatedOutputData.TotalTokens);                    
                }
            }
            var dataset = new DataSet
            {
                Id = updateDto.Id,
                Name = updateDto.Name,
                Origin = updateDto.Origin,
                Description = updateDto.Description,
                AiModelType = updateDto.AiModelType,
                Tags = updateDto.Tags,
                DataObjectCount = updateDto.DataObjects?.Count ?? 0,
                TotalTokens = updateDto.DataObjects?.Sum(obj => obj.TotalTokens) ?? 0,
                TotalInputTokens = updateDto.DataObjects?.Sum(obj => obj.TotalInputTokens) ?? 0,
                TotalOutputTokens = updateDto.DataObjects?.Sum(obj => obj.TotalOutputTokens) ?? 0,
                TotalOutputTokensPerIndex = totalOutputTokensPerIndex,
                DaysToAutoDelete = updateDto.DaysToAutoDelete
            };
            
            // Delete existing data objects for this dataset
            if(updateDto.DataObjects?.Count > 0){
                await _dataObjectRepository.DeleteByDataSetIdAsync(id);
            }
            
            // If there are new data objects, add them
            if (updateDto.DataObjects?.Count > 0)
            {
                foreach (var obj in updateDto.DataObjects)
                {
                    obj.DataSetId = id;
                }
                await _dataObjectRepository.CreateManyAsync(updateDto.DataObjects);
            }
            
            // Update the dataset
            var updated = await _repository.UpdateAsync(dataset);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            // First delete all associated data objects
            await _dataObjectRepository.DeleteByDataSetIdAsync(id);
            
            // Then delete the dataset
            await _repository.DeleteAsync(id);
            
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting dataset: {ex}");
            return BadRequest(new { message = ex.Message, details = ex.ToString() });
        }
    }

    [HttpGet("{id}/dataobjects")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<DataObject>>> GetDataObjects(string id)
    {
        try
        {
            var dataObjects = await _dataObjectRepository.GetByDataSetIdAsync(id);
            return Ok(dataObjects);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/dataobjects")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> AddDataObjects(string id, [FromBody] List<DataObject> dataObjects)
    {
        try
        {
            var dataset = await _repository.GetByIdAsync(id);
            
            if (dataObjects == null || !dataObjects.Any())
            {
                return BadRequest("No data objects provided");
            }
            
            // Process data objects to calculate tokens
            await ProcessDataObjectsAsync(dataObjects);
            
            // Set the DataSetId and populate OriginalDataFile/OriginalIndex for each object
            var currentDataset = await _repository.GetByIdAsync(id);
            var existingDataObjects = await _dataObjectRepository.GetByDataSetIdAsync(id);
            var currentIndex = existingDataObjects.Count(); // Start index from existing count
            
            foreach (var obj in dataObjects)
            {
                obj.DataSetId = id;
                
                // Set OriginalDataFile if not already set
                if (string.IsNullOrEmpty(obj.OriginalDataFile))
                {
                    obj.OriginalDataFile = currentDataset.DataFiles?.FirstOrDefault()?.FileName ?? "manual-entry";
                }
                
                // Set OriginalIndex if not already set
                if (obj.OriginalIndex == -1)
                {
                    obj.OriginalIndex = currentIndex++;
                }
            }
            
            // Add the data objects
            await _dataObjectRepository.CreateManyAsync(dataObjects);
            
            // Get all data objects for the dataset to accurately calculate totals
            var allDataObjects = await _dataObjectRepository.GetByDataSetIdAsync(id);
            
            // Calculate token counts for the dataset
            var totalOutputTokensPerIndex = dataset.TotalOutputTokensPerIndex ?? new Dictionary<string, int>();
            foreach (var dataObject in dataObjects)
            {
                var count = 0;
                foreach (var outputData in dataObject.OutputData)
                {
                    if (totalOutputTokensPerIndex.ContainsKey(count.ToString()))
                    {
                        totalOutputTokensPerIndex[count.ToString()] += outputData.TotalTokens;
                    }
                    else
                    {
                        totalOutputTokensPerIndex[count.ToString()] = outputData.TotalTokens;
                    }
                    count++;
                }
                
                foreach (var generatedOutputData in dataObject.GeneratedOutputData)
                {
                    if (totalOutputTokensPerIndex.ContainsKey(generatedOutputData.GeneratedForClinicalTask))
                    {
                        totalOutputTokensPerIndex[generatedOutputData.GeneratedForClinicalTask] += generatedOutputData.TotalTokens;
                    }
                    else
                    {
                        totalOutputTokensPerIndex[generatedOutputData.GeneratedForClinicalTask] = generatedOutputData.TotalTokens;
                    }
                }
            }
            
            // Update the dataset with new counts
            dataset.TotalTokens = allDataObjects.Sum(obj => obj.TotalTokens);
            dataset.TotalInputTokens = allDataObjects.Sum(obj => obj.TotalInputTokens);
            dataset.TotalOutputTokens = allDataObjects.Sum(obj => obj.TotalOutputTokens);
            dataset.TotalOutputTokensPerIndex = totalOutputTokensPerIndex;
            dataset.DataObjectCount = allDataObjects.Count();
            
            // Update the dataset
            var updated = await _repository.UpdateAsync(dataset);
            
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{dataSetId}/dataobjects/{dataObjectId}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<DataObject>> GetDataObject(string dataSetId, string dataObjectId)
    {
        try
        {
            // First verify the dataset exists
            await _repository.GetByIdAsync(dataSetId);
            
            // Then get the specific data object
            var dataObject = await _dataObjectRepository.GetByIdAsync(dataObjectId);
            
            // Verify this object belongs to the requested dataset
            if (dataObject.DataSetId != dataSetId)
            {
                return NotFound();
            }
            
            return Ok(dataObject);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id}/dataobjects")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> UpdateDataObjects(string id, [FromBody] List<DataObject> dataObjects)
    {
        try
        {
            var dataset = await _repository.GetByIdAsync(id);
            
            if (dataObjects == null || !dataObjects.Any())
            {
                return BadRequest("No data objects provided");
            }
            
            // Process data objects to calculate tokens
            await ProcessDataObjectsAsync(dataObjects);
            
            // Set the DataSetId for each object
            foreach (var obj in dataObjects)
            {
                obj.DataSetId = id;
            }
            
            // Update the data objects
            await _dataObjectRepository.UpdateManyAsync(dataObjects);
            
            // Calculate token counts for the dataset
            var totalOutputTokensPerIndex = new Dictionary<string, int>();
            foreach (var dataObject in dataObjects)
            {
                var count = 0;
                foreach (var outputData in dataObject.OutputData)
                {
                    if (totalOutputTokensPerIndex.ContainsKey(count.ToString()))
                    {
                        totalOutputTokensPerIndex[count.ToString()] += outputData.TotalTokens;
                    }
                    else
                    {
                        totalOutputTokensPerIndex[count.ToString()] = outputData.TotalTokens;
                    }
                    count++;
                }
                
                foreach (var generatedOutputData in dataObject.GeneratedOutputData)
                {
                    if (totalOutputTokensPerIndex.ContainsKey(generatedOutputData.GeneratedForClinicalTask))
                    {
                        totalOutputTokensPerIndex[generatedOutputData.GeneratedForClinicalTask] += generatedOutputData.TotalTokens;
                    }
                    else
                    {
                        totalOutputTokensPerIndex[generatedOutputData.GeneratedForClinicalTask] = generatedOutputData.TotalTokens;
                    }
                }
            }
            
            // Get all data objects for the dataset to accurately calculate totals
            var allDataObjects = await _dataObjectRepository.GetByDataSetIdAsync(id);
            
            // Update the dataset with new counts
            dataset.TotalTokens = allDataObjects.Sum(obj => obj.TotalTokens);
            dataset.TotalInputTokens = allDataObjects.Sum(obj => obj.TotalInputTokens);
            dataset.TotalOutputTokens = allDataObjects.Sum(obj => obj.TotalOutputTokens);
            dataset.TotalOutputTokensPerIndex = totalOutputTokensPerIndex;
            
            // Update the dataset
            await _repository.UpdateAsync(dataset);
            
            return Ok(dataset);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id}/datafiles")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<DataFileDto>>> GetDataFiles(string id)
    {
        try
        {
            var dataset = await _repository.GetByIdAsync(id);
            var dataFileDtos = dataset.DataFiles.Select(df => new DataFileDto
            {
                FileName = df.FileName,
                ProcessingStatus = df.ProcessingStatus.ToString(),
                ErrorMessage = df.ErrorMessage,
                UploadedAt = df.UploadedAt,
                ProcessedObjectCount = df.ProcessedObjectCount,
                TotalObjectCount = df.TotalObjectCount,
                Mapping = new DataFileMappingDto
                {
                    InputMappings = df.Mapping.InputMappings.Select(m => new DataFileKeyPathDto { Type = m.Type, KeyPath = m.KeyPath, IsArray = m.IsArray }).ToList(),
                    OutputMappings = df.Mapping.OutputMappings.Select(m => new DataFileKeyPathDto { Type = m.Type, KeyPath = m.KeyPath, IsArray = m.IsArray }).ToList()
                }
            });
            
            return Ok(dataFileDtos);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/datafiles")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult> AddDataFile(string id, [FromForm] IFormFile file, [FromForm] string mapping)
    {
        try
        {
            var dataset = await _repository.GetByIdAsync(id);
            
            // Deserialize the mapping
            var dataFileMapping = JsonSerializer.Deserialize<DataFileMapping>(mapping);
            
            // Validate mapping if dataset already has datafiles
            if (dataset.DataFiles.Any())
            {
                var existingMapping = dataset.DataFiles.FirstOrDefault()?.Mapping;
                if (existingMapping?.InputMappings.Count != dataFileMapping?.InputMappings.Count ||
                    existingMapping?.OutputMappings.Count != dataFileMapping?.OutputMappings.Count)
                {
                    return BadRequest("New file mapping must match existing file mappings");
                }
            }
            
            // Upload the file to Azure Blob Storage
            var blobUrl = await _dataFileService.UploadDataFileAsync(file, id);
            
            // Create a new DataFile entry
            var dataFile = new DataFile
            {
                FileName = file.FileName,
                BlobUrl = blobUrl,
                ProcessingStatus = DataFileProcessingStatus.Unprocessed,
                UploadedAt = DateTime.UtcNow,
                Mapping = dataFileMapping ?? new DataFileMapping() // Provide default if null
            };
            
            // Add the datafile to the dataset
            dataset.DataFiles.Add(dataFile);
            await _repository.UpdateAsync(dataset);
            
            // Start processing the file asynchronously
            _ = Task.Run(async () => 
            {
                try
                {
                    await _dataFileService.ProcessDataFileAsync(dataset, dataFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing datafile {FileName}", file.FileName);
                }
            });
            
            return Ok(new { message = "File uploaded and processing started", dataFileId = dataset.DataFiles.IndexOf(dataFile) });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding datafile");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}/datafiles/{fileIndex}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult> DeleteDataFile(string id, int fileIndex)
    {
        try
        {
            var dataset = await _repository.GetByIdAsync(id);
            
            if (fileIndex < 0 || fileIndex >= dataset.DataFiles.Count)
            {
                return BadRequest("Invalid file index");
            }
            
            var dataFile = dataset.DataFiles[fileIndex];
            
            // Delete the file from Azure Blob Storage
            await _dataFileService.DeleteDataFileAsync(dataFile.BlobUrl);
            
            // Remove data objects associated with this file
            // This requires tracking which data objects came from which file
            // For simplicity, we'll delete all data objects for now
            await _dataObjectRepository.DeleteByDataSetIdAsync(id);
            
            // Remove the datafile from the dataset
            dataset.DataFiles.RemoveAt(fileIndex);
            
            // Update dataset counts
            dataset.DataObjectCount = 0;
            dataset.TotalTokens = 0;
            await _repository.UpdateAsync(dataset);
            
            // Re-process the remaining files to rebuild the data objects
            foreach (var remainingFile in dataset.DataFiles)
            {
                remainingFile.ProcessingStatus = DataFileProcessingStatus.Unprocessed;
                remainingFile.ProcessedObjectCount = 0;
            }
            await _repository.UpdateAsync(dataset);
            
            // Start processing the files asynchronously
            _ = Task.Run(async () => 
            {
                foreach (var remainingFile in dataset.DataFiles)
                {
                    try
                    {
                        await _dataFileService.ProcessDataFileAsync(dataset, remainingFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error re-processing datafile {FileName}", remainingFile.FileName);
                    }
                }
            });
            
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting datafile");
            return BadRequest(new { message = ex.Message });
        }
    }
}
