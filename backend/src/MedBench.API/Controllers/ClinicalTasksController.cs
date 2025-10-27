using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Services;

using Azure.Storage.Blobs;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MedBench.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClinicalTasksController : ControllerBase
{
    private readonly IClinicalTaskRepository _clinicalTaskRepository;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ClinicalTasksController> _logger;
    private readonly IDataSetRepository _dataSetRepository;
    private readonly IModelRepository _modelRepository;
    private readonly ITrialRepository _trialRepository;
    private readonly IExperimentRepository _experimentRepository;
    private readonly ITestScenarioRepository _testScenarioRepository;
    private readonly IConfiguration _configuration;

    public ClinicalTasksController(IClinicalTaskRepository clinicalTaskRepository, IServiceScopeFactory serviceScopeFactory, ILogger<ClinicalTasksController> logger, IDataSetRepository dataSetRepository, IModelRepository modelRepository, ITrialRepository trialRepository, IExperimentRepository experimentRepository, ITestScenarioRepository testScenarioRepository, IConfiguration configuration)
    {
        _clinicalTaskRepository = clinicalTaskRepository;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _dataSetRepository = dataSetRepository;
        _modelRepository = modelRepository;
        _trialRepository = trialRepository;
        _experimentRepository = experimentRepository;
        _testScenarioRepository = testScenarioRepository;
        _configuration = configuration;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<ClinicalTask>>> GetAll()
    {
        var tasks = await _clinicalTaskRepository.GetAllAsync();
        return Ok(tasks);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<ClinicalTask>> Get(string id)
    {
        try
        {
            var task = await _clinicalTaskRepository.GetByIdAsync(id);
            return Ok(task);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<ClinicalTask>> Create(ClinicalTask clinicalTask)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }
        clinicalTask.OwnerId = userId;
        var created = await _clinicalTaskRepository.CreateAsync(clinicalTask);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<ClinicalTask>> Update(string id, ClinicalTask clinicalTask)
    {
        try
        {
            clinicalTask.Id = id;
            clinicalTask.MetricsGenerationStatus = "idle";
            clinicalTask.GenerationStatus = "idle";
            var updated = await _clinicalTaskRepository.UpdateAsync(clinicalTask);
            return Ok(updated);
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
            await _clinicalTaskRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/generate")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult> GenerateOutputs(string id)
    {
        try
        {
            var task = await _clinicalTaskRepository.GetByIdAsync(id);
            
            // Update status to Processing
            task.GenerationStatus = "processing";
            await _clinicalTaskRepository.UpdateAsync(task);
            
            // Start background processing without awaiting
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var clinicalTaskRepo = scope.ServiceProvider.GetRequiredService<IClinicalTaskRepository>();
                var dataObjectRepo = scope.ServiceProvider.GetRequiredService<IDataObjectRepository>();
                var modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();
                var processingService = scope.ServiceProvider.GetRequiredService<IExperimentProcessingService>();
                
                try
                {
                    // Generate outputs similar to ExperimentProcessingService
                    await processingService.GenerateClinicalTaskOutputs(id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating outputs for clinical task {Id}", id);
                    
                    // Update task status to error
                    var taskToUpdate = await clinicalTaskRepo.GetByIdAsync(id);
                    taskToUpdate.GenerationStatus = "error";
                    await clinicalTaskRepo.UpdateAsync(taskToUpdate);
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
            _logger.LogError(ex, "Error initiating output generation for clinical task {Id}", id);
            return StatusCode(500);
        }
    }

    [HttpPost("estimate-cost")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<double>> EstimateCost([FromBody] CostEstimateRequest request)
    {
        try
        {
            var dataSet = await _dataSetRepository.GetByIdAsync(request.DataSetId);
            var model = await _modelRepository.GetByIdAsync(request.ModelId);
            
            // Calculate costs using both input and output token rates
            double cost = (dataSet.TotalInputTokens * model.CostPerToken) + 
                          (dataSet.TotalOutputTokens * model.CostPerTokenOut);
            
            return Ok(cost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating cost");
            return StatusCode(500);
        }
    }

    [HttpGet("pending-for-user")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult<IEnumerable<ClinicalTask>>> GetPendingTasksForUser()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            
            // Get all pending trials for this user
            var pendingTrials = await _trialRepository.GetPendingTrialsAsync(userId);
            var experimentIds = pendingTrials.Select(t => t.ExperimentId).Distinct().ToList();
            
            // Get experiments
            var experiments = new List<Experiment>();
            foreach (var expId in experimentIds)
            {
                try
                {
                    var exp = await _experimentRepository.GetByIdAsync(expId);
                    if (exp != null)
                    {
                        experiments.Add(exp);
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Skip experiments that no longer exist
                    continue;
                }
            }
            
            // Get test scenarios
            var scenarioIds = experiments.Select(e => e.TestScenarioId).Distinct().ToList();
            var scenarios = new List<TestScenario>();
            foreach (var scenId in scenarioIds)
            {
                try
                {
                    var scenario = await _testScenarioRepository.GetByIdAsync(scenId);
                    if (scenario != null)
                    {
                        scenarios.Add(scenario);
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Skip scenarios that no longer exist
                    continue;
                }
            }
            
            // Get clinical tasks
            var taskIds = scenarios.Select(s => s.TaskId).Distinct().ToList();
            var tasks = new List<ClinicalTask>();
            foreach (var taskId in taskIds)
            {
                try
                {
                    var task = await _clinicalTaskRepository.GetByIdAsync(taskId);
                    if (task != null)
                    {
                        tasks.Add(task);
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Skip tasks that no longer exist
                    continue;
                }
            }
            
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending tasks for user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/generate-metrics")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult> GenerateMetrics(string id)
    {
        try
        {
            var task = await _clinicalTaskRepository.GetByIdAsync(id);
            
            // Check if this clinical task has ground truth
            bool hasGroundTruth = task.DataSetModels.Any(dm => dm.IsGroundTruth);
            if (!hasGroundTruth)
            {
                return BadRequest("Cannot generate metrics for a clinical task without ground truth");
            }
            
            // Start background processing without awaiting
            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var clinicalTaskRepo = scope.ServiceProvider.GetRequiredService<IClinicalTaskRepository>();
                var dataObjectRepo = scope.ServiceProvider.GetRequiredService<IDataObjectRepository>();
                var processingService = scope.ServiceProvider.GetRequiredService<IExperimentProcessingService>();
                
                try
                {
                    // Update metrics status to Processing
                    var taskToUpdate = await clinicalTaskRepo.GetByIdAsync(id);
                    taskToUpdate.MetricsGenerationStatus = "processing";
                    await clinicalTaskRepo.UpdateAsync(taskToUpdate);
                    
                    // Get all data objects for this task
                    var dataSetIds = taskToUpdate.DataSetModels.Select(dm => dm.DataSetId).Distinct().ToList();
                    var allDataObjects = new List<DataObject>();
                    
                    foreach (var dataSetId in dataSetIds)
                    {
                        var dataObjects = await dataObjectRepo.GetByDataSetIdAsync(dataSetId);
                        allDataObjects.AddRange(dataObjects);
                    }
                    
                    // Track the metrics files we're generating
                    var metricsFiles = new List<(string modelId, string fileName)>();
                    
                    // Generate metrics for each model in the task
                    foreach (var dataSetModel in taskToUpdate.DataSetModels.Where(dm => !dm.IsGroundTruth && (!string.IsNullOrEmpty(dm.GeneratedOutputKey) || dm.ModelOutputIndex != -1)))
                    {
                        var modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();
                        var model = await modelRepo.GetByIdAsync(dataSetModel.ModelId);
                        
                        // Generate metrics file
                        string fileName = await processingService.GenerateMetricsJsonFile(
                            taskToUpdate, 
                            model, 
                            allDataObjects, 
                            dataSetModel.GeneratedOutputKey, 
                            dataSetModel.ModelOutputIndex
                        );
                        
                        // Track this file for checking results later
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            metricsFiles.Add((dataSetModel.ModelId, fileName));
                        }
                    }
                    
                    taskToUpdate.Metrics = new Dictionary<string, Dictionary<string, double>>();
                    
                    // Wait for and process results files
                    await MonitorAndProcessMetricsResults(taskToUpdate, metricsFiles, clinicalTaskRepo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating metrics for clinical task {Id}", id);
                    
                    // Update metrics task status to error
                    var taskToUpdate = await clinicalTaskRepo.GetByIdAsync(id);
                    taskToUpdate.MetricsGenerationStatus = "error";
                    await clinicalTaskRepo.UpdateAsync(taskToUpdate);
                }
            });
            
            return Ok(new { message = "Metrics generation started" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating metrics generation for clinical task {Id}", id);
            return StatusCode(500);
        }
    }

    // New method to monitor for results and update the clinical task
    private async Task MonitorAndProcessMetricsResults(
        ClinicalTask clinicalTask, 
        List<(string modelId, string fileName)> metricsFiles,
        IClinicalTaskRepository clinicalTaskRepo)
    {
        if (metricsFiles.Count == 0)
        {
            Console.WriteLine("No metrics files to monitor, updating status to complete");
            // No metrics files to monitor, update status to complete
            clinicalTask.MetricsGenerationStatus = "complete";
            await clinicalTaskRepo.UpdateAsync(clinicalTask);
            return;
        }
        
        try
        {
            // Get the blob service client
            var blobServiceClient = new BlobServiceClient(
                Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") 
                    ?? _configuration["AzureStorage:ConnectionString"]
            );
            
            // Get the metrics results container
            var resultsContainerClient = blobServiceClient.GetBlobContainerClient("metricresults");
            await resultsContainerClient.CreateIfNotExistsAsync();
            
            // This is not enough for large datasets.
            // Maximum time to wait for results (30 minutes)
            var timeout = TimeSpan.FromMinutes(30);
            var startTime = DateTime.UtcNow;
            bool allResultsFound = false;
            
            // Keep checking until we find all results or hit the timeout
            while (!allResultsFound && DateTime.UtcNow - startTime < timeout)
            {
                // Wait before checking
                await Task.Delay(TimeSpan.FromSeconds(5));
                
                foreach (var (modelId, fileName) in metricsFiles)
                {
                    // Check for the results file
                    // Handle both potential naming patterns
                    string resultFileName = fileName + "-results.json";
                    
                    BlobClient resultBlob = resultsContainerClient.GetBlobClient(resultFileName);
                    if (!await resultBlob.ExistsAsync())
                    {
                        allResultsFound = false;
                        continue;
                    }

                    // Check if the blob has content
                    var properties = await resultBlob.GetPropertiesAsync();
                    if (properties.Value.ContentLength == 0)
                    {
                        allResultsFound = false;
                        continue;
                    }
                    
                    // If we have the results file but haven't processed it yet
                    if (!clinicalTask.Metrics.ContainsKey(modelId))
                    {
                        // Download and process the results file
                        var download = await resultBlob.DownloadAsync();
                        
                        using (var streamReader = new StreamReader(download.Value.Content))
                        {
                            try{
                                var jsonContent = await streamReader.ReadToEndAsync();
                                var resultObject = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                                if (resultObject.TryGetProperty("metrics_results", out JsonElement metricsResults))
                                {
                                    // Convert the metrics results to a dictionary
                                    var metrics = new Dictionary<string, double>();
                                    Console.WriteLine("got metrics:"+ metricsResults.ToString());
                                    // Handle the case when metrics_results is an array
                                    if( metricsResults.TryGetProperty("aggregated_metrics", out JsonElement aggregatedMetrics)){
                                        foreach (var property in aggregatedMetrics.EnumerateObject())
                                        {
                                            if (property.Value.ValueKind == JsonValueKind.Number)
                                            {
                                                metrics[property.Name] = property.Value.GetDouble();
                                            }
                                        }
                                    }
                                    clinicalTask.Metrics[modelId] = metrics;
                                    // Update the clinical task
                                    await clinicalTaskRepo.UpdateAsync(clinicalTask);
                                }else{
                                     clinicalTask.MetricsGenerationStatus = "error";
                                    Console.WriteLine("Error parsing metrics results:"+ resultObject.ToString());
                                    break;
                                }
                            }
                            catch(Exception ex){
                                Console.WriteLine("Error parsing metrics results:"+ ex.Message);
                                clinicalTask.MetricsGenerationStatus = "error";
                                await clinicalTaskRepo.UpdateAsync(clinicalTask);
                                return;
                            }
                            
                            
                        }
                    }
                    if( clinicalTask.Metrics.Count == metricsFiles.Count){
                        allResultsFound = true;
                    }
                }
                
                // If we found all results, break out of the loop
                if (allResultsFound)
                {
                    break;
                }
            }
            
            // Update status to complete
            clinicalTask.MetricsGenerationStatus = "complete";
            await clinicalTaskRepo.UpdateAsync(clinicalTask);
            
            // Trigger model results calculation for each model to update rankings
            foreach (var (modelId, _) in metricsFiles)
            {
                try
                {
                    // Find any experiment that uses this clinical task to get experiment context
                    var testScenarios = await _testScenarioRepository.GetByClinicalTaskIdsAsync(new List<string> { clinicalTask.Id });
                    if (testScenarios.Any())
                    {
                        var experiments = await _experimentRepository.GetByTestScenarioIdsAsync(testScenarios.Select(ts => ts.Id).ToList());
                        if (experiments.Any())
                        {
                            _logger.LogInformation($"Triggering model results calculation for model {modelId} using experiment {experiments.First().Id}");
                            
                            // Use service scope to get StatCalculatorService
                            using var scope = _serviceScopeFactory.CreateScope();
                            var statCalculatorService = scope.ServiceProvider.GetRequiredService<StatCalculatorService>();
                            await statCalculatorService.CalculateModelResults(modelId, experiments.First().Id);
                        }
                    }
                }
                catch (Exception statEx)
                {
                    _logger.LogError(statEx, "Error calculating model results for task {TaskId}, model {ModelId}", clinicalTask.Id, modelId);
                }
            }
            
            // Programmatically trigger the same logic as "Upload Metrics" to ensure 
            // the metrics appear in rankings
            try
            {
                _logger.LogInformation("Automatically uploading metrics for clinical task {Id} to ensure they appear in rankings", clinicalTask.Id);
                
                // Call the same logic as the UploadMetrics endpoint
                // This ensures the metrics are properly processed for rankings display
                var updatedTask = await clinicalTaskRepo.GetByIdAsync(clinicalTask.Id);
                if (updatedTask.Metrics != null && updatedTask.Metrics.Count > 0)
                {
                    // Re-save the metrics to trigger any additional processing
                    updatedTask.MetricsGenerationStatus = "complete";
                    await clinicalTaskRepo.UpdateAsync(updatedTask);
                    _logger.LogInformation("Successfully uploaded metrics for clinical task {Id}", clinicalTask.Id);
                }
            }
            catch (Exception uploadEx)
            {
                _logger.LogError(uploadEx, "Error auto-uploading metrics for clinical task {Id}", clinicalTask.Id);
                // Don't fail the whole process if auto-upload fails
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring metrics results for clinical task {Id}", clinicalTask.Id);
            clinicalTask.MetricsGenerationStatus = "error";
            await clinicalTaskRepo.UpdateAsync(clinicalTask);
        }
    }

    [HttpPost("{id}/upload-metrics")]
    [Authorize(Policy = "RequireAuthenticatedUser")]
    public async Task<ActionResult> UploadMetrics(string id, [FromBody] Dictionary<string, Dictionary<string, double>> metrics)
    {
        try
        {
            var task = await _clinicalTaskRepository.GetByIdAsync(id);
            
            // Update the metrics
            task.Metrics = metrics;
            task.MetricsGenerationStatus = "complete";
            
            await _clinicalTaskRepository.UpdateAsync(task);
            
            return Ok(new { message = "Metrics uploaded successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading metrics for clinical task {Id}", id);
            return StatusCode(500);
        }
    }

    public class CostEstimateRequest
    {
        public string DataSetId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
    }
} 