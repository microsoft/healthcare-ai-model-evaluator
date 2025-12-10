//In the frontend this is called Assignments, but in the backend it's Experiments
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Extensions;
using MedBench.Core.Services;
using MedBench.Core.Repositories;
using SharpToken;
namespace MedBench.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExperimentsController : ControllerBase
{
    private readonly IExperimentRepository _experimentRepository;
    private readonly ITrialRepository _trialRepository;
    private readonly ITestScenarioRepository _testScenarioRepository;
    private readonly IClinicalTaskRepository _clinicalTaskRepository;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly ILogger<ExperimentsController> _logger;
    private readonly IExperimentProcessingService _experimentProcessingService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserRepository _userRepository;
    private readonly IModelRepository _modelRepository;
    private readonly IDataObjectRepository _dataObjectRepository;
    private readonly IImageRepository _imageRepository;

    public ExperimentsController(
        IExperimentRepository experimentRepository,
        ITrialRepository trialRepository,
        ITestScenarioRepository testScenarioRepository,
        IClinicalTaskRepository clinicalTaskRepository,
        IDataSetRepository dataSetRepository,
        ILogger<ExperimentsController> logger,
        IExperimentProcessingService experimentProcessingService,
        IServiceScopeFactory scopeFactory,
        IUserRepository userRepository,
        IModelRepository modelRepository,
        IDataObjectRepository dataObjectRepository,
        IImageRepository imageRepository)
    {
        _experimentRepository = experimentRepository;
        _trialRepository = trialRepository;
        _testScenarioRepository = testScenarioRepository;
        _clinicalTaskRepository = clinicalTaskRepository;
        _dataSetRepository = dataSetRepository;
        _logger = logger;
        _experimentProcessingService = experimentProcessingService;
        _scopeFactory = scopeFactory;
        _userRepository = userRepository;
        _modelRepository = modelRepository;
        _dataObjectRepository = dataObjectRepository;
        _imageRepository = imageRepository;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<Experiment>>> GetAll()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var experiments = await _experimentRepository.GetAllAsync();
        return Ok(experiments);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<Experiment>> Get(string id)
    {
        try
        {
            var experiment = await _experimentRepository.GetByIdAsync(id);
            

            return Ok(experiment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<Experiment>> Create([FromBody] Experiment experiment)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Ensure we have an ID before saving
            if (string.IsNullOrEmpty(experiment.Id))
            {
                experiment.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            }

            var userId = User.GetUserId();
            experiment.OwnerId = userId;
            experiment.Status = ExperimentStatus.Draft;
            experiment.ProcessingStatus = ProcessingStatus.NotProcessed;
            experiment.CreatedAt = DateTime.UtcNow;
            experiment.UpdatedAt = DateTime.UtcNow;

            // Calculate trials and cost if test scenario is provided
            if (!string.IsNullOrEmpty(experiment.TestScenarioId))
            {
                var testScenario = await _testScenarioRepository.GetByIdAsync(experiment.TestScenarioId);
                var modelRepo = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IModelRepository>();
                
                if (testScenario != null && !string.IsNullOrEmpty(testScenario.TaskId))
                {
                    var clinicalTask = await _clinicalTaskRepository.GetByIdAsync(testScenario.TaskId);
                    var models = new List<MedBench.Core.Models.Model>();
                    foreach (var modelId in testScenario.ModelIds)
                    {
                        var model = await modelRepo.GetByIdAsync(modelId);
                        if (model != null)
                            models.Add(model);
                    }

                    if (clinicalTask != null && clinicalTask.DataSetModels.Any())
                    {
                        var dataSet = await _dataSetRepository.GetByIdAsync(clinicalTask.DataSetModels.First().DataSetId);
                        if (dataSet != null)
                        {
                            experiment.TotalTrials = dataSet.DataObjectCount * experiment.ReviewerIds.Count;
                            if (experiment.ExperimentType == "Arena")
                            {
                                experiment.TotalTrials = dataSet.DataObjectCount * testScenario.ModelIds.Count;
                            }
                            experiment.TotalCost = await CalculateTotalCost(experiment, testScenario, dataSet, models);
                        }
                    }
                }
            }

            Console.WriteLine($"Creating experiment with ID: {experiment.Id}");
            var created = await _experimentRepository.CreateAsync(experiment);
            
            // Verify the object has an ID
            if (string.IsNullOrEmpty(created.Id))
            {
                Console.WriteLine("Error: Created experiment has no ID");
                return StatusCode(500, new { message = "Failed to generate ID for experiment" });
            }
            
            // Return just the ID to avoid potential serialization issues
            return CreatedAtAction(
                actionName: nameof(Get), 
                routeValues: new { id = created.Id }, 
                value: new { id = created.Id }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating experiment: {ex.Message}");
            return StatusCode(500, new { message = $"Error creating experiment: {ex.Message}" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> Update(string id, Experiment experiment)
    {
        try
        {
            var existing = await _experimentRepository.GetByIdAsync(id);
            
            if (existing.Status != ExperimentStatus.Draft)
                return BadRequest("Only draft experiments can be edited");

            if (existing.ProcessingStatus == ProcessingStatus.Final)
            {
                experiment.ProcessingStatus = ProcessingStatus.NotProcessed;
                await _trialRepository.DeleteByExperimentIdAsync(id);
            }

            await _experimentRepository.UpdateAsync(experiment);
            return Ok(experiment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            // First delete all associated trials
            await _trialRepository.DeleteByExperimentIdAsync(id);
            
            // Then delete the experiment
            await _experimentRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting experiment {Id}", id);
            return StatusCode(500, "An error occurred while deleting the experiment");
        }
    }

    [HttpGet("assigned")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<Experiment>>> GetAssigned()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var experiments = await _experimentRepository.GetByUserIdAsync(userId);
        var assignedExperiments = experiments.Where(e => 
            e.ReviewerIds.Contains(userId) && e.OwnerId != userId);
        return Ok(assignedExperiments);
    }

    [HttpPut("{id}/process")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<IActionResult> ProcessExperiment(string id)
    {
        try
        {
            var experiment = await _experimentRepository.GetByIdAsync(id);
            
            if (experiment.Status != ExperimentStatus.Draft)
                return BadRequest("Only draft experiments can be processed");
            
            if (experiment.ProcessingStatus == ProcessingStatus.Processing)
                return BadRequest("Experiment is already processing");

            // Update status to Processing
            experiment.ProcessingStatus = ProcessingStatus.Processing;
            await _experimentRepository.UpdateAsync(experiment);
            
            // Kick off background processing without awaiting
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var experimentRepo = scope.ServiceProvider.GetRequiredService<IExperimentRepository>();
                    var clinicalTaskRepo = scope.ServiceProvider.GetRequiredService<IClinicalTaskRepository>();
                    var dataObjectRepo = scope.ServiceProvider.GetRequiredService<IDataObjectRepository>();
                    var trialRepo = scope.ServiceProvider.GetRequiredService<ITrialRepository>();
                    var processingService = scope.ServiceProvider.GetRequiredService<IExperimentProcessingService>();

                    var exp = await experimentRepo.GetByIdAsync(id);
                    if (exp != null && exp.ProcessingStatus == ProcessingStatus.Processing)
                    {
                        _logger.LogInformation($"Starting background processing for experiment {id}");
                        await processingService.ProcessExperiment(exp, clinicalTaskRepo, dataObjectRepo, trialRepo, experimentRepo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error in background processing for experiment {id}");
                }
            });
            
            return Ok(experiment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<Experiment>> UpdateStatus(string id, [FromBody] ExperimentStatus status)
    {
        try
        {
            var experiment = await _experimentRepository.GetByIdAsync(id);
            if (experiment == null)
                return NotFound();

            experiment.Status = status;
            
            // Update all trials associated with this experiment
            await _trialRepository.UpdateExperimentStatusAsync(id, status.ToString());

            if (status == ExperimentStatus.Completed)
            {
                //experiment.ProcessingStatus = ProcessingStatus.Finalizing;
                experiment.PendingTrials = 0;
            }
            else if (status == ExperimentStatus.Cancelled)
            {
                experiment.PendingTrials = 0;
            }
            else if (status == ExperimentStatus.InProgress)
            {
                experiment.PendingTrials = experiment.TotalTrials;
                _ = Task.Run(async () =>
                {
                    await _experimentProcessingService.ProcessModelReviewers(id);
                });
            }
            
            var updated = await _experimentRepository.UpdateAsync(experiment);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating experiment status");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}/export")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult> ExportExperiment(string id)
    {
        try
        {
            var experiment = await _experimentRepository.GetByIdAsync(id);
            if (experiment == null)
                return NotFound();

            // Get trials for this experiment
            var trials = await _trialRepository.GetByExperimentIdAsync(id);
            
            // Enrich trials with image blob URLs
            var enrichedTrials = new List<object>();
            foreach (var trial in trials)
            {
                DataObject? dataObject = null;
                if (!string.IsNullOrEmpty(trial.DataObjectId))
                {
                    dataObject = await _dataObjectRepository.GetByIdWithIndexAsync(trial.DataObjectId);
                }
                var enrichedTrial = new
                {
                    id = trial.Id,
                    userId = trial.UserId,
                    experimentId = trial.ExperimentId,
                    experimentType = trial.ExperimentType,
                    status = trial.Status,
                    experimentStatus = trial.ExperimentStatus,
                    prompt = trial.Prompt,
                    modelInputs = await EnrichDataContentWithImages(trial.ModelInputs),
                    originalDataObject = dataObject,
                    flags = trial.Flags,
                    dataSetId = trial.DataSetId,
                    modelOutputs = await EnrichModelOutputsWithImages(trial.ModelOutputs),
                    response = trial.Response,
                    trialResponse = trial.TrialResponse,
                    reviewerInstructions = trial.ReviewerInstructions,
                    createdAt = trial.CreatedAt,
                    updatedAt = trial.UpdatedAt,
                    questions = trial.Questions
                };
                enrichedTrials.Add(enrichedTrial);
            }
            
            // Get unique data object IDs and dataset IDs from trials
            var dataObjectIds = new HashSet<string>();
            var dataSetIds = new HashSet<string>();
            
            foreach (var trial in trials)
            {
                if (!string.IsNullOrEmpty(trial.DataObjectId))
                {
                    dataObjectIds.Add(trial.DataObjectId);
                }
                if (!string.IsNullOrEmpty(trial.DataSetId))
                {
                    dataSetIds.Add(trial.DataSetId);
                }
            }
            
            // Fetch all data objects with enriched image data
            var enrichedDataObjects = new List<object>();
            foreach (var dataSetId in dataSetIds)
            {
                try
                {
                    var dataObjects = await _dataObjectRepository.GetByDataSetIdAsync(dataSetId);
                    var filteredObjects = dataObjects.Where(obj => dataObjectIds.Contains(obj.Id));
                    
                    foreach (var dataObject in filteredObjects)
                    {
                        var enrichedObject = await EnrichDataObjectWithImageBlobs(dataObject);
                        enrichedDataObjects.Add(enrichedObject);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to fetch data objects for dataset {dataSetId}: {ex.Message}");
                }
            }
            
            var exportData = new
            {
                experiment = new
                {
                    id = experiment.Id,
                    name = experiment.Name,
                    description = experiment.Description,
                    status = experiment.Status,
                    processingStatus = experiment.ProcessingStatus,
                    testScenarioId = experiment.TestScenarioId,
                    experimentType = experiment.ExperimentType,
                    tags = experiment.Tags,
                    reviewerIds = experiment.ReviewerIds,
                    createdAt = experiment.CreatedAt,
                    updatedAt = experiment.UpdatedAt,
                    pendingTrials = experiment.PendingTrials,
                    totalTrials = experiment.TotalTrials,
                    totalCost = experiment.TotalCost,
                    reviewerInstructions = experiment.ReviewerInstructions
                },
                trials = enrichedTrials,
                dataObjects = enrichedDataObjects,
                exportedAt = DateTime.UtcNow,
                exportType = "experiment"
            };

            return Ok(exportData);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting experiment {Id}", id);
            return StatusCode(500, "An error occurred while exporting the experiment");
        }
    }

    private async Task<object> EnrichDataObjectWithImageBlobs(DataObject dataObject)
    {
        var enrichedInputData = await EnrichDataContentWithImages(dataObject.InputData);
        var enrichedOutputData = await EnrichDataContentWithImages(dataObject.OutputData);
        var enrichedGeneratedOutputData = await EnrichDataContentWithImages(dataObject.GeneratedOutputData);

        return new
        {
            id = dataObject.Id,
            dataSetId = dataObject.DataSetId,
            name = dataObject.Name,
            description = dataObject.Description,
            inputData = enrichedInputData,
            outputData = enrichedOutputData,
            generatedOutputData = enrichedGeneratedOutputData,
            createdAt = dataObject.CreatedAt,
            updatedAt = dataObject.UpdatedAt,
            totalTokens = dataObject.TotalTokens,
            totalInputTokens = dataObject.TotalInputTokens,
            totalOutputTokens = dataObject.TotalOutputTokens,
            totalOutputTokensPerIndex = dataObject.TotalOutputTokensPerIndex
        };
    }

    private async Task<List<object>> EnrichDataContentWithImages(List<DataContent> dataContentList)
    {
        var enrichedList = new List<object>();

        foreach (var content in dataContentList)
        {
            var imageBlobUrl = await GetImageBlobUrl(content);
            
            // If this is an image and we have the content as an image ID, construct the API URL
            var contentUrl = content.ContentUrl;
            if ((content.Type?.ToLower() == "image" || content.Type?.ToLower() == "imageurl") 
                && string.IsNullOrEmpty(contentUrl) 
                && !string.IsNullOrEmpty(content.Content))
            {
                contentUrl = $"/api/images/{content.Content}";
            }
            
            var enrichedContent = new
            {
                type = content.Type,
                content = content.Content,
                generatedForClinicalTask = content.GeneratedForClinicalTask,
                totalTokens = content.TotalTokens,
                contentUrl = contentUrl,
                imageBlobUrl = imageBlobUrl
            };
            enrichedList.Add(enrichedContent);
        }

        return enrichedList;
    }

    private async Task<List<object>> EnrichModelOutputsWithImages(List<ModelOutput> modelOutputs)
    {
        var enrichedOutputs = new List<object>();

        foreach (var output in modelOutputs)
        {
            var enrichedOutput = new
            {
                modelId = output.ModelId,
                output = await EnrichDataContentWithImages(output.Output)
            };
            enrichedOutputs.Add(enrichedOutput);
        }

        return enrichedOutputs;
    }

    private async Task<string?> GetImageBlobUrl(DataContent content)
    {
        // Check if this content is an image
        if (content.Type?.ToLower() == "image" || content.Type?.ToLower() == "imageurl")
        {
            try
            {
                string? imageId = null;
                
                // Try to get image ID from ContentUrl first
                if (!string.IsNullOrEmpty(content.ContentUrl))
                {
                    var urlParts = content.ContentUrl.Split('/');
                    imageId = urlParts.LastOrDefault();
                }
                
                // If ContentUrl is empty, try to use the Content field as the image ID
                if (string.IsNullOrEmpty(imageId) && !string.IsNullOrEmpty(content.Content))
                {
                    imageId = content.Content;
                }
                
                if (!string.IsNullOrEmpty(imageId))
                {
                    var image = await _imageRepository.GetByIdAsync(imageId);
                    
                    // Return the full blob URL instead of just the blob path
                    // Format: https://{storageAccount}.blob.core.windows.net/{container}/{blobPath}
                    if (!string.IsNullOrEmpty(image.StorageAccount) && !string.IsNullOrEmpty(image.Container))
                    {
                        return $"https://{image.StorageAccount}.blob.core.windows.net/{image.Container}/{image.BlobPath}";
                    }
                    else
                    {
                        // Fallback to just the blob path if storage account info is not available
                        return image.BlobPath;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to get blob key for image: {ex.Message}");
            }
        }

        return null;
    }

    private bool IsValidStatusTransition(ExperimentStatus currentStatus, ExperimentStatus newStatus, ProcessingStatus processingStatus)
    {
        if (currentStatus == newStatus) return true;

        return (currentStatus, newStatus, processingStatus) switch
        {
            (ExperimentStatus.Draft, ExperimentStatus.InProgress, ProcessingStatus.Final) => true,
            (ExperimentStatus.InProgress, ExperimentStatus.Completed, _) => true,
            (ExperimentStatus.InProgress, ExperimentStatus.Cancelled, _) => true,
            _ => false
        };
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
    private async Task<double> CalculateTotalCost(
        Experiment experiment, 
        TestScenario testScenario, 
        DataSet dataSet, 
        List<MedBench.Core.Models.Model> models)
    {
        double totalCost = 0;

        // Calculate cost for model outputs using both input and output token rates
        foreach (var model in models)
        {
            totalCost += (dataSet.TotalInputTokens * model.CostPerToken) + 
                         (dataSet.TotalOutputTokens * model.CostPerTokenOut);
        }

        // Get model reviewers from ReviewerIds
        var modelReviewers = await _userRepository.GetModelReviewersFromIds(experiment.ReviewerIds);
        
        foreach (var reviewer in modelReviewers)
        {
            if (string.IsNullOrEmpty(reviewer.ModelId)) continue;
            
            var reviewerModel = await _modelRepository.GetByIdAsync(reviewer.ModelId);
            if (reviewerModel == null) continue;

            // Calculate tokens for a sample prompt
            var sampleTrial = new Trial 
            { 
                ReviewerInstructions = testScenario.ReviewerInstructions,
                ModelInputs = new List<DataContent> { new() { Type = "text", Content = "sample" } },
                ModelOutputs = new List<ModelOutput> { new() { Output = new List<DataContent> { new() { Content = "sample" } } } }
            };

            var prompt = await _experimentProcessingService.BuildPromptForExperimentType(
                experiment.ExperimentType, 
                testScenario, 
                sampleTrial
            );

            var promptTokens = CountTokens(prompt);
            
            // For reviewer models, we estimate output tokens as a portion of input tokens
            var estimatedOutputTokens = promptTokens / 2; // A reasonable estimate

            // Calculate total tokens for all trials this reviewer will process
            var totalInputTokens = promptTokens * dataSet.DataObjectCount;
            var totalOutputTokens = estimatedOutputTokens * dataSet.DataObjectCount;
            
            if (experiment.ExperimentType == "Arena")
            {
                totalInputTokens *= testScenario.ModelIds.Count;
                totalOutputTokens *= testScenario.ModelIds.Count;
            }

            totalCost += (totalInputTokens * reviewerModel.CostPerToken) + 
                         (totalOutputTokens * reviewerModel.CostPerTokenOut);
        }

        return totalCost;
    }
} 