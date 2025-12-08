using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedBench.Core.Services;

public class DataRetentionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataRetentionBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Run daily
    private readonly TimeSpan _runTime = TimeSpan.FromHours(2); // Run at 2 AM

    public DataRetentionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DataRetentionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data retention background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = CalculateNextRunTime(now);

            var delay = nextRun - now;
            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation($"Next data retention cleanup scheduled for: {nextRun:yyyy-MM-dd HH:mm:ss} UTC");
                await Task.Delay(delay, stoppingToken);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await RunDataRetentionCleanup(stoppingToken);
            }
        }
    }

    private DateTime CalculateNextRunTime(DateTime currentTime)
    {
        var today = currentTime.Date;
        var nextRun = today.Add(_runTime);

        // If we've already passed today's run time, schedule for tomorrow
        if (currentTime >= nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun;
    }

    private async Task RunDataRetentionCleanup(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Data retention cleanup started at: {DateTime.UtcNow}");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dataSetRepository = scope.ServiceProvider.GetRequiredService<IDataSetRepository>();
            var dataObjectRepository = scope.ServiceProvider.GetRequiredService<IDataObjectRepository>();
            var clinicalTaskRepository = scope.ServiceProvider.GetRequiredService<IClinicalTaskRepository>();
            var trialRepository = scope.ServiceProvider.GetRequiredService<ITrialRepository>();
            var experimentRepository = scope.ServiceProvider.GetRequiredService<IExperimentRepository>();
            var testScenarioRepository = scope.ServiceProvider.GetRequiredService<ITestScenarioRepository>();
            var blobService = scope.ServiceProvider.GetRequiredService<IDataFileService>();

            // Get all datasets that should be evaluated for deletion
            var allDataSets = await dataSetRepository.GetAllAsync();
            var datasetsToProcess = allDataSets.Where(ds => ds.DeletedAt == null).ToList();
            
            _logger.LogInformation($"Found {datasetsToProcess.Count} datasets to evaluate for retention cleanup");

            int deletedCount = 0;

            foreach (var dataSet in datasetsToProcess)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var daysSinceCreation = (DateTime.UtcNow - dataSet.CreatedAt).Days;
                    
                    if (daysSinceCreation >= dataSet.DaysToAutoDelete)
                    {
                        _logger.LogInformation($"Deleting dataset '{dataSet.Name}' (ID: {dataSet.Id}) - {daysSinceCreation} days old, retention: {dataSet.DaysToAutoDelete} days");
                        
                        await DeleteDataSetAndAssociatedData(
                            dataSet, 
                            dataSetRepository,
                            dataObjectRepository,
                            clinicalTaskRepository,
                            trialRepository,
                            experimentRepository,
                            testScenarioRepository,
                            blobService);
                        
                        deletedCount++;
                    }
                    else
                    {
                        _logger.LogDebug($"Dataset '{dataSet.Name}' (ID: {dataSet.Id}) - {daysSinceCreation} days old, retention: {dataSet.DaysToAutoDelete} days - no action needed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing dataset {dataSet.Id} for retention cleanup");
                }
            }

            _logger.LogInformation($"Data retention cleanup completed. Deleted {deletedCount} datasets.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data retention cleanup process");
        }
    }

    private async Task DeleteDataSetAndAssociatedData(
        DataSet dataSet,
        IDataSetRepository dataSetRepository,
        IDataObjectRepository dataObjectRepository,
        IClinicalTaskRepository clinicalTaskRepository,
        ITrialRepository trialRepository,
        IExperimentRepository experimentRepository,
        ITestScenarioRepository testScenarioRepository,
        IDataFileService dataFileService)
    {
        _logger.LogInformation($"Starting deletion of dataset {dataSet.Id} and all associated data");

        try
        {
            // 1. Delete blob storage files
            foreach (var dataFile in dataSet.DataFiles ?? new List<DataFile>())
            {
                try
                {
                    await dataFileService.DeleteDataFileAsync(dataFile.BlobUrl);
                    _logger.LogDebug($"Deleted blob file: {dataFile.FileName}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete blob file {dataFile.FileName}");
                }
            }

            // 2. Delete data objects and their associated blob references
            var dataObjects = await dataObjectRepository.GetByDataSetIdAsync(dataSet.Id);
            foreach (var dataObject in dataObjects)
            {
                try
                {
                    // Delete blob storage files referenced in data objects
                    await DeleteBlobReferencesFromDataObject(dataObject, dataFileService);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete blob references for data object {dataObject.Id}");
                }
            }
            
            // Delete all data objects for this dataset
            await dataObjectRepository.DeleteByDataSetIdAsync(dataSet.Id);

            // 3. Delete clinical tasks that reference this dataset  
            // Note: Simplified - may need to implement proper dataset-to-task relationship tracking
            // var clinicalTasks = await clinicalTaskRepository.GetAllAsync();
            // var tasksToDelete = clinicalTasks.Where(ct => /* relationship logic */).ToList();
            var tasksToDelete = new List<ClinicalTask>(); // Placeholder for now

            foreach (var task in tasksToDelete)
            {
                try
                {
                    await clinicalTaskRepository.DeleteAsync(task.Id);
                    _logger.LogDebug($"Deleted clinical task: {task.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete clinical task {task.Id}");
                }
            }

            // 4. Delete test scenarios that reference the deleted clinical tasks
            // Note: Simplified - may need to implement proper task-to-scenario relationship tracking
            // var testScenarios = await testScenarioRepository.GetAllAsync();
            // var scenariosToDelete = testScenarios.Where(ts => /* relationship logic */).ToList();
            var scenariosToDelete = new List<TestScenario>(); // Placeholder for now

            foreach (var scenario in scenariosToDelete)
            {
                try
                {
                    await testScenarioRepository.DeleteAsync(scenario.Id);
                    _logger.LogDebug($"Deleted test scenario: {scenario.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete test scenario {scenario.Id}");
                }
            }

            // 5. Delete experiments that reference the deleted test scenarios
            var experiments = await experimentRepository.GetAllAsync();
            var experimentsToDelete = experiments.Where(e => 
                scenariosToDelete.Any(s => s.Id == e.TestScenarioId)).ToList();

            foreach (var experiment in experimentsToDelete)
            {
                try
                {
                    await experimentRepository.DeleteAsync(experiment.Id);
                    _logger.LogDebug($"Deleted experiment: {experiment.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete experiment {experiment.Id}");
                }
            }

            // 6. Delete trials that reference the deleted experiments
            var trials = await trialRepository.GetAllAsync();
            var trialsToDelete = trials.Where(t => 
                experimentsToDelete.Any(e => e.Id == t.ExperimentId)).ToList();

            foreach (var trial in trialsToDelete)
            {
                try
                {
                    await trialRepository.DeleteAsync(trial.Id);
                    _logger.LogDebug($"Deleted trial: {trial.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete trial {trial.Id}");
                }
            }

            // 7. Finally, mark the dataset as deleted (soft delete)
            dataSet.DeletedAt = DateTime.UtcNow;
            await dataSetRepository.UpdateAsync(dataSet);

            _logger.LogInformation($"Successfully completed deletion of dataset {dataSet.Id} and associated data. " +
                $"Deleted: {dataObjects.Count()} data objects, {tasksToDelete.Count} clinical tasks, " +
                $"{scenariosToDelete.Count} test scenarios, {experimentsToDelete.Count} experiments, " +
                $"{trialsToDelete.Count} trials");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting dataset {dataSet.Id} and associated data");
            throw;
        }
    }

    private async Task DeleteBlobReferencesFromDataObject(DataObject dataObject, IDataFileService dataFileService)
    {
        var allDataContent = new List<DataContent>();
        allDataContent.AddRange(dataObject.InputData ?? new List<DataContent>());
        allDataContent.AddRange(dataObject.OutputData ?? new List<DataContent>());
        allDataContent.AddRange(dataObject.GeneratedOutputData ?? new List<DataContent>());

        foreach (var content in allDataContent)
        {
            if (content.Type == "imageurl" && !string.IsNullOrEmpty(content.Content))
            {
                try
                {
                    // Extract blob name from URL and delete
                    var blobName = ExtractBlobNameFromUrl(content.Content);
                    if (!string.IsNullOrEmpty(blobName))
                    {
                        await dataFileService.DeleteDataFileAsync(content.Content);
                        _logger.LogDebug($"Deleted blob referenced by data object: {blobName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete blob from data object content: {content.Content}");
                }
            }
        }
    }

    private string ExtractBlobNameFromUrl(string blobUrl)
    {
        try
        {
            var uri = new Uri(blobUrl);
            return Path.GetFileName(uri.LocalPath);
        }
        catch
        {
            return string.Empty;
        }
    }
}