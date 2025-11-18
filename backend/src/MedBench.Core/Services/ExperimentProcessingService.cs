using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Microsoft.Extensions.Hosting;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using SharpToken;
using System.Text.Json.Nodes;

namespace MedBench.Core.Services
{

    public interface IExperimentProcessingService
    {
        Task CollateExperimentResults(string experimentId);
        Task ProcessExperiment(Experiment experiment, IClinicalTaskRepository clinicalTaskRepo, 
            IDataObjectRepository dataObjectRepo, ITrialRepository trialRepo, IExperimentRepository experimentRepo);
        Task ProcessModelReviewers(string experimentId);
        Task<string> BuildPromptForExperimentType(string experimentType, TestScenario scenario, Trial trial, bool includeInputData = true);
        Task<string> BuildBasePromptForExperimentType(TestScenario scenario, Trial trial, bool includeInputData = true);
        string BuildOutputInstructionsForExperimentType(string experimentType);
        Task GenerateClinicalTaskOutputs(string clinicalTaskId);
        Task<string> GenerateMetricsJsonFile(ClinicalTask clinicalTask, MedBench.Core.Models.Model model, List<DataObject> dataObjects, string generatedOutputKey, int modelOutputIndex);
    }

    public class ExperimentProcessingService : BackgroundService, IExperimentProcessingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExperimentProcessingService> _logger;
        private readonly IImageService _imageService;
        private readonly ITestScenarioRepository _testScenarioRepository;
        private readonly IUserRepository _userRepository;
        private readonly IModelRepository _modelRepository;
        private readonly ITrialRepository _trialRepository;
        private readonly IExperimentRepository _experimentRepository;
        private readonly IModelRunnerFactory _modelRunnerFactory;
        private readonly StatCalculatorService _statCalculatorService;
        private readonly IImageRepository _imageRepository;
        private readonly GptEncoding _encoding;
    // Track currently processing experiments (value unused). Concurrent to avoid race conditions
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _processingExperiments = new();

        public ExperimentProcessingService(
            IServiceScopeFactory scopeFactory,
            ILogger<ExperimentProcessingService> logger,
            IImageService imageService,
            ITestScenarioRepository testScenarioRepository,
            IUserRepository userRepository,
            IModelRepository modelRepository,
            ITrialRepository trialRepository,
            IExperimentRepository experimentRepository,
            IModelRunnerFactory modelRunnerFactory,
            IImageRepository imageRepository,
            StatCalculatorService statCalculatorService,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _imageService = imageService;
            _testScenarioRepository = testScenarioRepository;
            _userRepository = userRepository;
            _modelRepository = modelRepository;
            _trialRepository = trialRepository;
            _experimentRepository = experimentRepository;
            _modelRunnerFactory = modelRunnerFactory;
            _imageRepository = imageRepository;
            _statCalculatorService = statCalculatorService;
            _encoding = GptEncoding.GetEncoding("cl100k_base");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Polling disabled – processing now only starts via explicit controller trigger
            _logger.LogInformation("Experiment Processing Service idle (poller disabled).");
            try
            {
                // Stay alive until cancellation requested
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // normal shutdown
            }
            _logger.LogInformation("Experiment Processing Service stopping (idle mode)");
        }

        public async Task ProcessExperiment(
            Experiment experiment,
            IClinicalTaskRepository clinicalTaskRepo,
            IDataObjectRepository dataObjectRepo,
            ITrialRepository trialRepo,
            IExperimentRepository experimentRepo)
        {
            // Concurrency guard: ensure only one active processing task per experiment
            if (!_processingExperiments.TryAdd(experiment.Id, 0))
            {
                _logger.LogInformation("Experiment {ExperimentId} is already being processed; skipping duplicate trigger.", experiment.Id);
                return;
            }
            int totalTrials = 0;
            List<IModelRunner> modelRunners = new();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var testScenarioRepo = scope.ServiceProvider.GetRequiredService<ITestScenarioRepository>();
                var modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();
                
                _logger.LogInformation($"Loading test scenario and clinical task for experiment {experiment.Id}");
                var testScenario = await testScenarioRepo.GetByIdAsync(experiment.TestScenarioId);
                var clinicalTask = await clinicalTaskRepo.GetByIdAsync(testScenario.TaskId);

                // Get models that need integration
                
                var modelsNeedingIntegration = (await Task.WhenAll(testScenario.ModelIds
                    .Select(modelId => modelRepo.GetByIdAsync(modelId))))
                    .Where(m => m.IntegrationType is "cxrreportgen" or "openai" or "openai-reasoning" or "azure-serverless" or "functionapp")
                    .ToList();  
                Console.WriteLine($"Models needing integration: {string.Join(", ", modelsNeedingIntegration.Select(m => m.Name))}");
                if (modelsNeedingIntegration.Any())
                {
                    foreach( var model in modelsNeedingIntegration){
                        modelRunners.Add(model.IntegrationType switch
                        {
                            "openai" => new OpenAIModelRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
                            "openai-reasoning" => new OpenAIReasoningModelRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
                            "cxrreportgen" => new CXRReportGenModelRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
                            "azure-serverless" => new AzureServelessEndpointRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
                            "functionapp" => new AzureFunctionAppRunner(model.IntegrationSettings, _imageService, _scopeFactory, _logger, model.Id),
                            _ => throw new ArgumentException($"Unknown model type: {model.IntegrationType}")
                        });
                    }
                }

                _logger.LogInformation($"Deleting existing trials for experiment {experiment.Id}");
                await trialRepo.DeleteByExperimentIdAsync(experiment.Id);

                if (experiment.ExperimentType == "Arena")
                {
                    _logger.LogInformation(
                        $"Processing AB experiment {experiment.Id} with {testScenario.ModelIds.Count} models");
                    totalTrials = await ProcessABExperiment(
                        experiment,
                        clinicalTask,
                        testScenario,
                        dataObjectRepo,
                        trialRepo,
                        modelRunners
                    );
                    experiment.TotalTrials = totalTrials;
                }
                else
                {
                    _logger.LogInformation($"Processing standard experiment {experiment.Id}");
                    var dataSetRepo = scope.ServiceProvider.GetRequiredService<IDataSetRepository>();
                    var genRunTime = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
                    foreach (var dataSetModel in clinicalTask.DataSetModels)
                    {
                        if (!testScenario.ModelIds.Contains(dataSetModel.ModelId))
                            continue;
                        var dataSet = await dataSetRepo.GetByIdAsync(dataSetModel.DataSetId);
                        var model = await modelRepo.GetByIdAsync(dataSetModel.ModelId);
                        var dataObjects = await dataObjectRepo.GetByDataSetIdAsync(dataSetModel.DataSetId);
                        var GeneratedOutputKey1 = dataSetModel.GeneratedOutputKey + genRunTime;
                        foreach (var dataObject in dataObjects)
                        {
                            var outputGenerated = false;
                            // Generate model output if needed
                            if (dataSetModel.IsGroundTruth == false && dataSetModel.GeneratedOutputKey == model.Name && dataSetModel.ModelOutputIndex == -1 && model.IntegrationType != null && modelRunners.Any())
                            {
                                try 
                                {
                                    var modelRunner = modelRunners.FirstOrDefault(mr => mr.ModelId == dataSetModel.ModelId);
                                    if (modelRunner != null)
                                    {
                                            // Process input data to handle images
                                            var processedInputData = await modelRunner.ProcessInputDataForModel(dataObject.InputData);
                                            
                                            // TODO: Implement batching logic to leverage async processing
                                            // Use separated prompts for generation
                                            var generatedOutput = await modelRunner.GenerateOutput(
                                                clinicalTask.Prompt ?? "",
                                                "", // No specific output instructions for standard experiment generation
                                                processedInputData,
                                                []
                                            );

                                            // Add to generated outputs
                                            var totalTokens = CountTokens(generatedOutput);
                                            dataObject.GeneratedOutputData.Add(new DataContent
                                            {
                                                Type = "text",
                                                Content = generatedOutput,
                                                TotalTokens = totalTokens,
                                                GeneratedForClinicalTask = GeneratedOutputKey1
                                            });
                                            outputGenerated = true;
                                            // Update data object
                                            await dataObjectRepo.UpdateManyAsync(new[] { dataObject });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Error generating output for data object {dataObject.Id}");
                                    throw;
                                }
                            }else{
                                _logger.LogInformation($"No integration needed for data object {dataObject.Id}");
                            }

                            foreach (var reviewerId in experiment.ReviewerIds)
                            {
                                var trial = new Trial
                                {
                                    UserId = reviewerId,
                                    ExperimentStatus = ExperimentStatus.Draft.ToString(),
                                    Status = "pending",
                                    Prompt = clinicalTask.Prompt ?? "",
                                    DataObjectId = dataObject.Id,
                                    DataSetId = dataSetModel.DataSetId,
                                    ExperimentId = experiment.Id,
                                    ReviewerInstructions = testScenario.ReviewerInstructions ?? "",
                                    ExperimentType = experiment.ExperimentType,
                                    ModelInputs = dataObject.InputData,
                                    ModelOutputs = new List<ModelOutput>(),
                                    BoundingBoxes = new List<BoundingBox>(),
                                    TestScenarioId = testScenario.Id,
                                    Questions = testScenario.Questions,
                                    AllowOutputEditing = testScenario.AllowOutputEditing
                                };

                                // Use generated output if available
                                if (dataObject.GeneratedOutputData.Count > 0 && dataObject.GeneratedOutputData.Any(g => g.GeneratedForClinicalTask == dataSetModel.GeneratedOutputKey))
                                {
                                    var rawOutput = dataObject.GeneratedOutputData.First(g => g.GeneratedForClinicalTask == dataSetModel.GeneratedOutputKey).Content;
                                    var (plainText, boundingBoxes) = ParseCXROutput(rawOutput, model.Id);
                                    trial.ModelOutputs.Add(new ModelOutput
                                    {
                                        ModelId = model.Id,
                                        Output = new List<DataContent> { new DataContent { Type = "text", Content = plainText } }
                                    });
                                    trial.BoundingBoxes.AddRange(boundingBoxes);
                                } 
                                else if (dataSetModel.ModelOutputIndex == -1 && dataObject.GeneratedOutputData.Any())
                                {
                                    var rawOutput = dataObject.GeneratedOutputData.Last().Content;
                                    var (plainText, boundingBoxes) = ParseCXROutput(rawOutput, model.Id);
                                    trial.ModelOutputs.Add(new ModelOutput
                                    {
                                        ModelId = model.Id,
                                        Output = new List<DataContent> { new DataContent { Type = "text", Content = plainText } }
                                    });
                                    trial.BoundingBoxes.AddRange(boundingBoxes);
                                }
                                else if (dataObject.OutputData.Count > dataSetModel.ModelOutputIndex)
                                {
                                    trial.ModelOutputs.Add(new ModelOutput
                                    {
                                        ModelId = model.Id,
                                        Output = new List<DataContent> { dataObject.OutputData[dataSetModel.ModelOutputIndex] }
                                    });
                                }

                                await trialRepo.CreateAsync(trial);
                                totalTrials++;
                            }
                            if (outputGenerated)
                            {
                                dataSet.GeneratedDataList.Add(GeneratedOutputKey1);
                                await dataSetRepo.UpdateAsync(dataSet);
                            }
                            
                        }
                        
                        
                        
                    }
                }

                // After creating all trials, count them
                var trials = await trialRepo.GetByExperimentIdAsync(experiment.Id);
                experiment.TotalTrials = trials.Count();
                experiment.PendingTrials = trials.Count(t => t.Status == "pending");
                experiment.ProcessingStatus = ProcessingStatus.Processed;
                await experimentRepo.UpdateAsync(experiment);
                _logger.LogInformation($"Successfully processed experiment {experiment.Id} with {experiment.TotalTrials} total trials");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing experiment {experiment.Id}");
                experiment.ProcessingStatus = ProcessingStatus.Error;
                experiment.TotalTrials = totalTrials;
                experiment.PendingTrials = 0;
                await experimentRepo.UpdateAsync(experiment);
            }
            finally
            {
                foreach (var modelRunner in modelRunners)
                {
                    modelRunner.Dispose();
                }
                // Remove from concurrency tracking
                _processingExperiments.TryRemove(experiment.Id, out _);
            }
        }

        
        private async Task<int> ProcessABExperiment(
            Experiment experiment,
            ClinicalTask clinicalTask,
            TestScenario testScenario,
            IDataObjectRepository dataObjectRepo,
            ITrialRepository trialRepo,
            List<IModelRunner> modelRunners)
        {
            using var scope = _scopeFactory.CreateScope();
            var dataSetRepo = scope.ServiceProvider.GetRequiredService<IDataSetRepository>();
            var modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();
            var genRunTime = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            var datasetGroups = clinicalTask.DataSetModels
                .Where(dm => testScenario.ModelIds.Contains(dm.ModelId))
                .ToList();

            _logger.LogInformation(
                $"Processing AB experiment {experiment.Id} with {datasetGroups.Count()} datasets");

            int totalTrialsCreated = 0;
            for (int i = 0; i < datasetGroups.Count; i++)
            {
                for (int j = i + 1; j < datasetGroups.Count; j++)
                {
                    var dataset1 = datasetGroups[i];
                    var dataset2 = datasetGroups[j];

                    var dataObjects1 = await dataObjectRepo.GetByDataSetIdAsync(dataset1.DataSetId);
                    var dataSet = await dataSetRepo.GetByIdAsync(dataset1.DataSetId);
                    var totalOutputTokensPerIndex = dataSet.TotalOutputTokensPerIndex;
                    var model1 = await modelRepo.GetByIdAsync(dataset1.ModelId);
                    var model2 = await modelRepo.GetByIdAsync(dataset2.ModelId);
                    
                    _logger.LogInformation(
                        $"Creating trials for dataset pair {dataset1.DataSetId}-{dataset1.ModelOutputIndex}-{dataset2.ModelId}-{dataset1.ModelOutputIndex} with {dataObjects1.Count()}");
                    var GeneratedOutputKey1 = dataset1.GeneratedOutputKey + genRunTime;
                    var GeneratedOutputKey2 = dataset2.GeneratedOutputKey + genRunTime;
                    
                    foreach (var dataObject in dataObjects1)
                    {
                        var outputGenerated1 = false;
                        var outputGenerated2 = false;
                        // Generate outputs if needed
                        if (dataset1.ModelOutputIndex == -1 && modelRunners.Any())
                        {
                            try
                            {
                                foreach( var modelRunner in modelRunners){
                                    if( dataset1.ModelId == modelRunner.ModelId ){
                                        if (dataset1.ModelOutputIndex == -1 && dataset1.GeneratedOutputKey == model1.Name)
                                        {
                                            // Process input data to handle images
                                            var processedInputData = await modelRunner.ProcessInputDataForModel(dataObject.InputData);
                                            // Use separated prompts for generation
                                            var output = await modelRunner.GenerateOutput(
                                                clinicalTask.Prompt ?? "",
                                                "", // No specific output instructions for AB experiment generation
                                                processedInputData,
                                                []
                                            );
                                            outputGenerated1 = true;
                                            var totalTokens = CountTokens(output);
                                            dataObject.GeneratedOutputData.Add(new DataContent
                                            {
                                                Type = "text",
                                                Content = output,
                                                TotalTokens = totalTokens,
                                                GeneratedForClinicalTask = GeneratedOutputKey1
                                            });
                                            totalOutputTokensPerIndex[GeneratedOutputKey1] = totalTokens;
                                        }
                                    }
                                    if( dataset2.ModelId == modelRunner.ModelId ){
                                        if (dataset2.ModelOutputIndex == -1 && dataset2.GeneratedOutputKey == model2.Name)
                                        {
                                            // Process input data to handle images
                                            var processedInputData = await modelRunner.ProcessInputDataForModel(dataObject.InputData);
                                            // Use separated prompts for generation
                                            var output = await modelRunner.GenerateOutput(
                                                clinicalTask.Prompt ?? "",
                                                "", // No specific output instructions for AB experiment generation
                                                processedInputData,
                                                []
                                            );
                                            outputGenerated2 = true;
                                            var totalTokens = CountTokens(output);
                                            dataObject.GeneratedOutputData.Add(new DataContent
                                            {
                                                Type = "text",
                                                Content = output,
                                                TotalTokens = totalTokens,
                                                GeneratedForClinicalTask = GeneratedOutputKey2
                                            });
                                            totalOutputTokensPerIndex[GeneratedOutputKey2] = totalTokens;
                                        }
                                    }
                                    
                                }

                                await dataObjectRepo.UpdateManyAsync(new[] { dataObject });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error generating output for data object {dataObject.Id}");
                                throw;
                            }
                        }
                        if (outputGenerated1)
                        {
                            dataSet.GeneratedDataList.Add(GeneratedOutputKey1);
                        }
                        if (outputGenerated2)
                        {
                            dataSet.GeneratedDataList.Add(GeneratedOutputKey2);
                        }
                        if (outputGenerated1 || outputGenerated2)
                        {
                            await dataSetRepo.UpdateAsync(dataSet);
                        }
                        foreach (var reviewerId in experiment.ReviewerIds)
                        {
                            await CreateABTrial(
                                experiment,
                                clinicalTask,
                                testScenario,
                                reviewerId,
                                (dataObject, dataset1.ModelOutputIndex, dataset1.ModelId, dataset1.GeneratedOutputKey),
                                (dataObject, dataset2.ModelOutputIndex, dataset2.ModelId, dataset2.GeneratedOutputKey),
                                trialRepo
                            );
                            totalTrialsCreated++;
                        }
                    }
                }
            }

            return totalTrialsCreated;
        }

        private async Task CreateABTrial(
            Experiment experiment,
            ClinicalTask clinicalTask,
            TestScenario testScenario,
            string reviewerId,
            (DataObject dataObject, int modelOutputIndex, string modelId, string generatedOutputKey) first,
            (DataObject dataObject, int modelOutputIndex, string modelId, string generatedOutputKey) second,
            ITrialRepository trialRepo)
        {
            var trial = new Trial
            {
                UserId = reviewerId,
                ExperimentStatus = ExperimentStatus.Draft.ToString(),
                Status = "pending",
                Prompt = clinicalTask.Prompt ?? "",
                ReviewerInstructions = testScenario.ReviewerInstructions ?? "",
                DataObjectId = first.dataObject.Id,
                DataSetId = first.dataObject.DataSetId,
                ExperimentId = experiment.Id,
                ExperimentType = experiment.ExperimentType,
                ModelInputs = first.dataObject.InputData,
                ModelOutputs = new List<ModelOutput>(),
                BoundingBoxes = new List<BoundingBox>(),
                TestScenarioId = testScenario.Id,
                Questions = testScenario.Questions
            };

            // Process first model output
            var firstOutput = new DataContent();
            if (first.dataObject.GeneratedOutputData.Count > 0 && first.dataObject.GeneratedOutputData.Any(g => g.GeneratedForClinicalTask == first.generatedOutputKey))
            {
                var rawOutput = first.dataObject.GeneratedOutputData.First(g => g.GeneratedForClinicalTask == first.generatedOutputKey).Content;
                var (plainText, boundingBoxes) = ParseCXROutput(rawOutput, first.modelId);
                firstOutput = new DataContent { Type = "text", Content = plainText };
                trial.BoundingBoxes.AddRange(boundingBoxes);
            }
            else if (first.modelOutputIndex == -1 && first.dataObject.GeneratedOutputData.Any())
            {
                var rawOutput = first.dataObject.GeneratedOutputData.Last().Content;
                var (plainText, boundingBoxes) = ParseCXROutput(rawOutput, first.modelId);
                firstOutput = new DataContent { Type = "text", Content = plainText };
                trial.BoundingBoxes.AddRange(boundingBoxes);
            }
            else if (first.dataObject.OutputData.Count > first.modelOutputIndex)
            {
                firstOutput = first.dataObject.OutputData[first.modelOutputIndex];
            }

            // Process second model output
            var secondOutput = new DataContent();
            if (second.dataObject.GeneratedOutputData.Count > 0 && second.dataObject.GeneratedOutputData.Any(g => g.GeneratedForClinicalTask == second.generatedOutputKey))
            {
                var rawOutput = second.dataObject.GeneratedOutputData.First(g => g.GeneratedForClinicalTask == second.generatedOutputKey).Content;
                var (plainText, boundingBoxes) = ParseCXROutput(rawOutput, second.modelId);
                secondOutput = new DataContent { Type = "text", Content = plainText };
                trial.BoundingBoxes.AddRange(boundingBoxes);
            }
            else if (second.modelOutputIndex == -1 && second.dataObject.GeneratedOutputData.Any())
            {
                var rawOutput = second.dataObject.GeneratedOutputData.Last().Content;
                var (plainText, boundingBoxes) = ParseCXROutput(rawOutput, second.modelId);
                secondOutput = new DataContent { Type = "text", Content = plainText };
                trial.BoundingBoxes.AddRange(boundingBoxes);
            }
            else if (second.dataObject.OutputData.Count > second.modelOutputIndex)
            {
                secondOutput = second.dataObject.OutputData[second.modelOutputIndex];
            }

            var modelOutput1 = new ModelOutput
            {
                ModelId = first.modelId,
                Output = new List<DataContent> { firstOutput }
            };
            
            var modelOutput2 = new ModelOutput
            {
                ModelId = second.modelId,
                Output = new List<DataContent> { secondOutput }
            };

            // Randomly determine order
            if (Random.Shared.Next(2) == 0)
            {
                trial.ModelOutputs.Add(modelOutput1);
                trial.ModelOutputs.Add(modelOutput2);
            }
            else
            {
                trial.ModelOutputs.Add(modelOutput2);
                trial.ModelOutputs.Add(modelOutput1);
            }

            await trialRepo.CreateAsync(trial);
        }

        public async Task CollateExperimentResults(string experimentId)
        {
            try
            {
                _logger.LogInformation($"Starting collation of results for experiment {experimentId}");
                using var scope = _scopeFactory.CreateScope();
                var experimentRepo = scope.ServiceProvider.GetRequiredService<IExperimentRepository>();
                var trialRepo = scope.ServiceProvider.GetRequiredService<ITrialRepository>();
                var modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();

                var experiment = await experimentRepo.GetByIdAsync(experimentId);
                if (experiment == null) throw new ArgumentException("Experiment not found");

                experiment.ProcessingStatus = ProcessingStatus.Finalizing;
                await experimentRepo.UpdateAsync(experiment);

                var trials = await trialRepo.GetByExperimentIdAsync(experimentId);
                _logger.LogInformation(
                    $"Found {trials.Count()} trials to process for experiment {experimentId}");

                var modelResults = new Dictionary<string, ModelMetricsAccumulator>();

                foreach (var trial in trials)
                {
                    foreach (var modelOutput in trial.ModelOutputs)
                    {
                        if (!modelResults.ContainsKey(modelOutput.ModelId))
                        {
                            modelResults[modelOutput.ModelId] = new ModelMetricsAccumulator();
                        }

                        var accumulator = modelResults[modelOutput.ModelId];

                        switch (trial.ExperimentType)
                        {
                            case "Arena":
                                ProcessArenaResult(trial, modelOutput, accumulator);
                                break;
                            case "Simple Validation":
                                ProcessValidationResult(trial, modelOutput, accumulator);
                                break;
                            case "Full Validation":
                                ProcessFullValidationResult(trial, modelOutput, accumulator);
                                break;
                            case "Simple Evaluation":
                                ProcessSimpleEvaluationResult(trial, modelOutput, accumulator);
                                break;
                            case "Single Evaluation":
                                // No specific processing needed for single evaluation
                                break;
                            
                        }
                    }
                }

                foreach (var (modelId, metrics) in modelResults)
                {
                    var model = await modelRepo.GetByIdAsync(modelId);
                    if (model != null)
                    {
                        model.ExperimentResults = new ModelExperimentResults
                        {
                            EloScore = metrics.CalculateEloScore(),
                            CorrectScore = metrics.CalculateCorrectScore(),
                            ValidationTime = metrics.CalculateAverageValidationTime(),
                            AverageRating = metrics.CalculateAverageRating()
                        };
                        await modelRepo.UpdateAsync(model);
                    }
                }

                experiment.ProcessingStatus = ProcessingStatus.Final;
                await experimentRepo.UpdateAsync(experiment);
                _logger.LogInformation(
                    $"Successfully collated results for experiment {experimentId}. Processed {modelResults.Count} models");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error collating results for experiment {experimentId}");
                using var scope = _scopeFactory.CreateScope();
                var experimentRepo = scope.ServiceProvider.GetRequiredService<IExperimentRepository>();
                var experiment = await experimentRepo.GetByIdAsync(experimentId);
                if (experiment != null)
                {
                    experiment.ProcessingStatus = ProcessingStatus.Error;
                    await experimentRepo.UpdateAsync(experiment);
                }
                throw;
            }
        }

        private class ModelMetricsAccumulator
        {
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int TotalValidations { get; set; }
            public int CorrectValidations { get; set; }
            public double TotalValidationTime { get; set; }
            public int ValidationCount { get; set; }
            public double TotalRating { get; set; }
            public int RatingCount { get; set; }

            public double CalculateEloScore() => 1500 + (Wins - Losses) * 32;
            public double CalculateCorrectScore() => TotalValidations > 0 ? (double)CorrectValidations / TotalValidations : 0;
            public double CalculateAverageValidationTime() => ValidationCount > 0 ? TotalValidationTime / ValidationCount : 0;
            public double CalculateAverageRating() => RatingCount > 0 ? TotalRating / RatingCount : 0;
        }

        private void ProcessArenaResult(Trial trial, ModelOutput modelOutput, ModelMetricsAccumulator accumulator)
        {
            if (trial.Response?.Text == null)
            {
                _logger.LogWarning($"Skipping arena trial {trial.Id} - no response text");
                return;
            }

            var preferredModelIds = trial.Response.ModelId?.Split(',');
            if (preferredModelIds?.Length != 2)
            {
                _logger.LogWarning($"Invalid model preference format in trial {trial.Id}");
                return;
            }

            if (preferredModelIds[0] == modelOutput.ModelId)
            {
                accumulator.Wins++;
            }
            else if (preferredModelIds[1] == modelOutput.ModelId)
            {
                accumulator.Losses++;
            }
        }

        private void ProcessValidationResult(Trial trial, ModelOutput modelOutput, ModelMetricsAccumulator accumulator)
        {
            if (trial.Response?.Text == null) return;

            accumulator.TotalValidations++;
            if (bool.TryParse(trial.Response.Text, out bool isCorrect) && isCorrect)
            {
                accumulator.CorrectValidations++;
            }
        }

        private void ProcessFullValidationResult(Trial trial, ModelOutput modelOutput, ModelMetricsAccumulator accumulator)
        {
            accumulator.TotalValidationTime += trial.TotalTime;
            accumulator.ValidationCount++;
        }

        private void ProcessSimpleEvaluationResult(Trial trial, ModelOutput modelOutput, ModelMetricsAccumulator accumulator)
        {
            if (trial.Response?.Text == null) return;

            accumulator.TotalValidationTime += trial.TotalTime;
            accumulator.ValidationCount++;
        }
        private string ExtractJsonFromResponse(string response)
        {
            // Find the first '{' from the beginning
            var startIndex = response.IndexOf('{');
            if (startIndex == -1) return response; // No opening brace found

            // Find the last '}' from the end
            var endIndex = response.LastIndexOf('}');
            if (endIndex == -1 || endIndex <= startIndex) return response; // No closing brace found or invalid order

            return response.Substring(startIndex, endIndex - startIndex + 1).Trim();
        }
        public async Task ProcessModelReviewers(string experimentId)
        {
            using var scope = _scopeFactory.CreateScope();
            var trialRepository = scope.ServiceProvider.GetRequiredService<ITrialRepository>();
            var experimentRepository = scope.ServiceProvider.GetRequiredService<IExperimentRepository>();
            var testScenarioRepository = scope.ServiceProvider.GetRequiredService<ITestScenarioRepository>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var modelRepository = scope.ServiceProvider.GetRequiredService<IModelRepository>();
            var modelRunnerFactory = scope.ServiceProvider.GetRequiredService<IModelRunnerFactory>();
            var statCalculatorService = scope.ServiceProvider.GetRequiredService<StatCalculatorService>();

            var experiment = await experimentRepository.GetByIdAsync(experimentId);
            var testScenario = await testScenarioRepository.GetByIdAsync(experiment.TestScenarioId);
            var modelReviewers = await userRepository.GetModelReviewers();

            foreach (var reviewer in modelReviewers)
            {
                if (string.IsNullOrEmpty(reviewer.ModelId)) continue;

                var model = await modelRepository.GetByIdAsync(reviewer.ModelId);
                var modelRunner = modelRunnerFactory.CreateModelRunner(model);

                // Get all pending trials for this reviewer
                var trials = await trialRepository.GetByExperimentIdAsync(experimentId);
                var pendingTrials = trials.Where(t => t.UserId == reviewer.Id && t.Status == "pending");
                
                foreach (var trial in pendingTrials)
                {
                    _logger.LogInformation("Trial: " + trial.Id);
                    _logger.LogInformation(trial.ToJson());
                    // Verify trial exists before processing
                    var existingTrial = await trialRepository.GetByIdAsync(trial.Id);
                    if (existingTrial == null)
                    {
                        _logger.LogWarning($"Trial {trial.Id} not found in database, skipping");
                        continue;
                    }
                    _logger.LogInformation($"Processing trial {existingTrial.Id} for reviewer {reviewer.Id}");

                    try
                    {
                        string response;

                        // Check if the model runner supports separated prompts
                        var basePrompt = await BuildBasePromptForExperimentType(testScenario, existingTrial, false);
                        var outputInstructions = BuildOutputInstructionsForExperimentType(experiment.ExperimentType);

                        // Use the new overload that accepts separate base prompt and output instructions
                        response = await modelRunner.GenerateOutput(basePrompt, outputInstructions, existingTrial.ModelInputs, existingTrial.ModelOutputs);

                        _logger.LogInformation("Response: " + response);
                        try
                        {
                            if (existingTrial.ExperimentType == "Single Evaluation")
                            {
                                // Extract JSON from response, handling markdown code blocks
                                var jsonContent = ExtractJsonFromResponse(response);
                                
                                if (JsonNode.Parse(jsonContent) is JsonObject responseJson)
                                {
                                    if (responseJson["corrected_output"] != null && existingTrial.AllowOutputEditing)
                                    {
                                        existingTrial.Response = new TrialResponse
                                        {
                                            ModelId = existingTrial.ModelOutputs.First().ModelId,
                                            Text = responseJson["corrected_output"]?.ToString() ?? string.Empty
                                        };
                                    }
                                    var index = 1;
                                    foreach (var question in existingTrial.Questions)
                                    {
                                        if (responseJson[index.ToString()] != null)
                                        {
                                            question.Response = responseJson[index.ToString()]?.ToString();
                                        }
                                        index++;
                                    }
                                }
                                else
                                {
                                    _logger.LogError("Response is not a valid JSON object: {Response}", response);
                                    throw new FormatException("Response is not a valid JSON object");
                                }
                                existingTrial.Status = "done";

                            }
                            else
                            {
                                // Handle other experiment types
                                var parsedResponse = ParseModelResponse(response, experiment.ExperimentType);
                                existingTrial.Status = "done";
                                existingTrial.Response = new TrialResponse
                                {
                                    ModelId = existingTrial.ModelOutputs.First().ModelId,
                                    Text = parsedResponse
                                };
                            }


                            await trialRepository.UpdateAsync(existingTrial);
                        }
                        catch (FormatException ex)
                        {
                            _logger.LogError(ex, "Error parsing model response");
                            existingTrial.TrialErrorText = response;
                            existingTrial.Status = "done";
                            await trialRepository.UpdateAsync(existingTrial);
                        }




                        foreach (var output in existingTrial.ModelOutputs)
                        {
                            _logger.LogInformation("calculating stats for: " + output.ToJson());
                            await statCalculatorService.CalculateModelResults(output.ModelId, experimentId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing trial {TrialId} with model reviewer", trial.Id);
                        existingTrial.Status = "done";
                        existingTrial.Response = new TrialResponse
                        {
                            ModelId = existingTrial.ModelOutputs.First().ModelId,
                            Text = "Error"
                        };
                        await trialRepository.UpdateAsync(existingTrial);
                    }
                }
            }

            // Check if experiment is complete
            var remainingTrials = await trialRepository.GetPendingTrialCountForExperiment(experimentId);
            _logger.LogInformation("Remaining trials: " + remainingTrials);
            if (remainingTrials == 0)
            {
                experiment.PendingTrials = 0;
                //experiment.Status = ExperimentStatus.Completed;
                // Calculate model results for each model in the trial
                await experimentRepository.UpdateAsync(experiment);
            }else{
                experiment.PendingTrials = remainingTrials;
                await experimentRepository.UpdateAsync(experiment);
            }
        }

        public async Task<string> BuildPromptForExperimentType(string experimentType, TestScenario scenario, Trial trial, bool includeInputData = true)
        {
            var basePrompt = await BuildBasePromptForExperimentType(scenario, trial, includeInputData);
            var outputInstructions = BuildOutputInstructionsForExperimentType(experimentType);
            return basePrompt + outputInstructions;
        }

        public async Task<string> BuildBasePromptForExperimentType(TestScenario scenario, Trial trial, bool includeInputData = true)
        {
            using var scope = _scopeFactory.CreateScope();
            var clinicalTaskRepo = scope.ServiceProvider.GetRequiredService<IClinicalTaskRepository>();
            ClinicalTask clinicalTask = await clinicalTaskRepo.GetByIdAsync(scenario.TaskId);
            var basePrompt = 
                "You are a model evaluator reviewing AI model outputs.\n" +
                "Review the following input and output according to these instructions:\n" +
                $"    {scenario.ReviewerInstructions}\n\n" +
                "Original prompt from scenario:\n" +
                $"    {clinicalTask.Prompt}\n\n";

            if (trial.ExperimentType == "Single Evaluation")
            {
                basePrompt = "You are a model evaluator reviewing AI model outputs.\n" +
                "Review the following input and output according to these instructions:\n" +
                $"    {scenario.ReviewerInstructions}\n\n" +
                "Original prompt from scenario:\n" +
                $"    {clinicalTask.Prompt}\n\n";
                if (trial.Questions != null && trial.Questions.Count > 0)
                {
                    basePrompt += "Please answer each of the following questions,";
                    basePrompt += "return your answers in a json object where the key is the index of the question and the value is your response.\n";
                    basePrompt += "each question may have a list of possible answers to chose from, if there is no list it is a free response question.\n";
                    basePrompt += "Your response must follow the acceptable response format and your answers must be restricted to the provided options when provided.\n";
                    basePrompt += "example acceptable response format:\n";
                    basePrompt += "{\"1\": \"response for question 1\",\"2\": \"response for question 2\"}";
                    var index = 1;
                    foreach (var question in trial.Questions)
                    {
                        basePrompt += $"Question {index}   - {question.QuestionText}\n";
                        if (question.Options != null && question.Options.Count > 0)
                        {
                            basePrompt += $"Options for Question {index}:\n";
                            foreach (var option in question.Options)
                            {
                                basePrompt += $"    - {option.Value}\n";
                            }
                        }
                        else
                        {
                            basePrompt += $"Question {index} is a free response question.\n";
                        }
                        index++;
                    }
                }
                if (trial.AllowOutputEditing)
                {
                    basePrompt += "Please attempt to follow the original prompt and fully correct the output provided maintaining the same format. Put the corrected output in key 'corrected_output' of the json object.\n";
                    if (trial.Questions != null && trial.Questions.Count > 0)
                    {
                        basePrompt += "This will be in addition to the answer keys that you'll provide in the json response example:";
                        basePrompt += "{\"1\": \"response for question 1\",\"2\": \"response for question 2\", \"corrected_output\": \"your corrected output here\"}";
                    }
                    else
                    {
                        basePrompt += "example output: \n";
                        basePrompt += "{\"corrected_output\": \"your corrected output here\"}";
                    }
                }
                
            }
            if (includeInputData)
                {
                    basePrompt += "Input Data:\n" +
                    $"{await FormatDataContent(trial.ModelInputs)}\n" +
                    "Model Output(s):\n" +
                    $"{FormatModelOutputs(trial.ModelOutputs)}\n";
                }

            return basePrompt;
        }
        //TODO allow dynamic questions to be added to the experiment type
        public string BuildOutputInstructionsForExperimentType(string experimentType)
        {
            switch (experimentType)
            {
                case "Arena":
                    return "Compare Model A and Model B outputs.\n" +
                        "Respond with exactly one of these options:\n" +
                        "    - 'A' if Model A's output is better\n" +
                        "    - 'B' if Model B's output is better\n" +
                        "    - 'both-good' if both outputs are good\n" +
                        "    - 'both-bad' if neither output is acceptable";

                case "Simple Evaluation":
                    return "Rate the model output on a scale of 1-5:\n" +
                        "    1: Unusable\n" +
                        "    2: Poor\n" +
                        "    3: Good\n" +
                        "    4: Excellent\n" +
                        "    5: Perfect\n" +
                        "Respond with only the number.";

                case "Simple Validation":
                    return "Is the model output correct and appropriate?\n" +
                        "Respond with exactly 'yes' or 'no'.";

                case "Full Validation":
                    return "Review and correct the model output.\n" +
                        "Provide the corrected version maintaining the same format.";
                case "Single Evaluation":
                    return "";//This is not relevant to single evaluation as it will have multiple questions
                default:
                    throw new ArgumentException($"Unknown experiment type: {experimentType}");
            }
        }

        private async Task<string>  FormatDataContent(List<DataContent> contents)
        {
            var formattedContents = new List<string>();
            foreach (var content in contents)
            {
                if (content.Type == "imageurl")
                {
                    try
                    {
                        // Get image from repository
                        var image = await _imageRepository.GetByIdAsync(content.Content);
                        // Get image stream
                        using var stream = await _imageService.GetImageStreamAsync(image);
                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        
                        // Convert to base64
                        var base64 = Convert.ToBase64String(memoryStream.ToArray());
                        formattedContents.Add($"Type: {content.Type}\nContent: data:image/{image.ContentType.Split('/')[1]};base64,{base64}\n");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing image {ImageId}", content.Content);
                        formattedContents.Add($"Type: {content.Type}\nContent: Error loading image\n");
                    }
                }
                else
                {
                    formattedContents.Add($"Type: {content.Type}\nContent: {content.Content}\n");
                }
            }
            return string.Join("\n", formattedContents);
        }

        private string FormatModelOutputs(List<ModelOutput> outputs)
        {
            return string.Join("\n\n", outputs.Select((o, i) => 
                $"Model {(char)('A' + i)}:\n{string.Join("\n", o.Output.Select(c => c.Content))}"));
        }

        private string ParseModelResponse(string response, string experimentType)
        {
            response = response.Trim().ToLower();

            switch (experimentType)
            {
                case "Arena":
                    if (new[] { "a", "b", "both-good", "both-bad" }.Contains(response))
                        return response;
                    throw new FormatException(response);

                case "Simple Evaluation":
                    if (int.TryParse(response, out int rating) && rating >= 1 && rating <= 5)
                        return rating.ToString();
                    throw new FormatException(response);

                case "Simple Validation":
                    if (new[] { "yes", "no" }.Contains(response)){
                        return response;
                    }else{
                        _logger.LogError("Invalid validation response format: {Response}", response);
                        throw new FormatException(response);
                    }
                    

                case "Full Validation":
                    return response; // No specific format requirements

                default:
                    throw new ArgumentException($"Unknown experiment type: {experimentType}");
            }
        }

        public async Task GenerateClinicalTaskOutputs(string clinicalTaskId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var clinicalTaskRepo = scope.ServiceProvider.GetRequiredService<IClinicalTaskRepository>();
                var dataSetRepo = scope.ServiceProvider.GetRequiredService<IDataSetRepository>();
                var dataObjectRepo = scope.ServiceProvider.GetRequiredService<IDataObjectRepository>();
                var modelRepo = scope.ServiceProvider.GetRequiredService<IModelRepository>();
                var modelRunnerFactory = scope.ServiceProvider.GetRequiredService<IModelRunnerFactory>();
                
                var clinicalTask = await clinicalTaskRepo.GetByIdAsync(clinicalTaskId);
                clinicalTask.GenerationStatus = "processing";
                await clinicalTaskRepo.UpdateAsync(clinicalTask);
                
                double totalCost = 0;
                var modelRunners = new List<IModelRunner>();
                
                // For each dataset-model pair
                foreach (var dataSetModel in clinicalTask.DataSetModels.Where(dm => !dm.IsGroundTruth && dm.ModelOutputIndex == -1))
                {
                    var model = await modelRepo.GetByIdAsync(dataSetModel.ModelId);
                    var dataSet = await dataSetRepo.GetByIdAsync(dataSetModel.DataSetId);
                    var dataObjects = await dataObjectRepo.GetByDataSetIdAsync(dataSetModel.DataSetId);
                    
                    if (model.IntegrationType != null)
                    {
                        try
                        {
                            var modelRunner = modelRunnerFactory.CreateModelRunner(model);
                            modelRunners.Add(modelRunner);
                            
                            var generatedOutputKey = $"{model.Name}_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}";
                            
                            foreach (var dataObject in dataObjects)
                            {
                                if (dataSetModel.ModelOutputIndex == -1) // Generate output
                                {
                                    // TODO: Implement batching logic to leverage async processing
                                    // Use separated prompts for generation
                                    var generatedOutput = await modelRunner.GenerateOutput(
                                        clinicalTask.Prompt ?? "",
                                        "", // No specific output instructions for clinical task generation
                                        dataObject.InputData,
                                        new List<ModelOutput>()
                                    );
                                    var totalTokens = CountTokens(generatedOutput);
                                    dataObject.GeneratedOutputData.Add(new DataContent
                                    {
                                        Type = "text",
                                        Content = generatedOutput,
                                        TotalTokens = totalTokens,
                                        GeneratedForClinicalTask = generatedOutputKey
                                    });
                                    
                                    dataObject.TotalOutputTokens += totalTokens;
                                    dataObject.TotalOutputTokensPerIndex[generatedOutputKey] = totalTokens;
                                    
                                    await dataObjectRepo.UpdateManyAsync(new[] { dataObject });
                                    
                                    // Calculate cost using both input and output token costs
                                    totalCost += (dataObject.TotalInputTokens * model.CostPerToken) + 
                                                 (dataObject.TotalOutputTokens * model.CostPerTokenOut);
                                }
                            }
                            
                            // Update dataset's generated data list
                            dataSet.GeneratedDataList.Add(generatedOutputKey);
                            await dataSetRepo.UpdateAsync(dataSet);
                            
                            // Update the generatedOutputKey in the clinical task
                            dataSetModel.GeneratedOutputKey = generatedOutputKey;                                                        
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error generating outputs for model {ModelId}", model.Id);
                            throw;
                        }
                    }
                }
                
                // Update clinical task
                clinicalTask.TotalCost = totalCost;
                clinicalTask.GenerationStatus = "complete";
                clinicalTask.MetricsGenerationStatus = "idle";
                await clinicalTaskRepo.UpdateAsync(clinicalTask);
                
                // Dispose model runners
                foreach (var runner in modelRunners)
                {
                    runner.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating clinical task outputs");
                
                // Try to update status to error
                using var scope = _scopeFactory.CreateScope();
                var clinicalTaskRepo = scope.ServiceProvider.GetRequiredService<IClinicalTaskRepository>();
                
                try
                {
                    var clinicalTask = await clinicalTaskRepo.GetByIdAsync(clinicalTaskId);
                    clinicalTask.GenerationStatus = "error";
                    await clinicalTaskRepo.UpdateAsync(clinicalTask);
                }
                catch
                {
                    // Ignore errors in error handling
                }
                
                throw;
            }
        }

        public async Task<string> GenerateMetricsJsonFile(ClinicalTask clinicalTask, MedBench.Core.Models.Model model, List<DataObject> dataObjects, string generatedOutputKey, int modelOutputIndex)
        {
            try
            {
                _logger.LogInformation($"Generating metrics JSON file for model {model.Name}");
                
                // Get the ground truth dataset model
                var groundTruthDataSetModel = clinicalTask.DataSetModels.FirstOrDefault(dm => dm.IsGroundTruth);
                var outputData = clinicalTask.DataSetModels.Where(dm => !dm.IsGroundTruth).ToList();
                
                // This data schema is only used here
                // Create metrics object following the testfile.json format
                var metricsJson = new
                {
                    metrics_type = MapEvalMetricToMetricsType(clinicalTask.EvalMetric),
                    model_run = new
                    {
                        id = $"{clinicalTask.Id}_{model.Id}",
                        model = new
                        {
                            name = model.Name,
                            version = model.IntegrationSettings.GetValueOrDefault("VERSION", DateTime.UtcNow.ToString("yyyy-MM-dd")),
                        },
                        dataset = new
                        {
                            name = $"clinical_task_{clinicalTask.Id}",
                            description = $"Dataset for clinical task {clinicalTask.Name}",
                            instances = dataObjects.Select((dataObject, index) => CreateInstanceFromDataObject(dataObject, clinicalTask.Prompt, groundTruthDataSetModel, index)).ToList()
                        },
                        results = dataObjects.Select((dataObject, index) => 
                        {
                            var generatedOutput = new DataContent();
                            if(modelOutputIndex == -1){
                                generatedOutput = dataObject.GeneratedOutputData
                                .FirstOrDefault(g => g.GeneratedForClinicalTask == generatedOutputKey);
                            }else{
                                generatedOutput = dataObject.OutputData[modelOutputIndex];
                            }
                            
                            return new
                            {
                                input_id = index,
                                completions = new
                                {
                                    content = new []
                                    {
                                        new
                                        {
                                            type = "Text",
                                            data = generatedOutput?.Content ?? "No generated output found",
                                            location = (string?)null,
                                            metadata = (object?)null,
                                            highlighted_segments = new object[] { }
                                        }
                                    }
                                },
                                finish_reason = "stop",
                                error = (string?)null
                            };
                        }).ToList()
                    }
                };
                
                // Convert to JSON
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(metricsJson, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                string blobName = "";
                
                try
                {
                    // Get the BlobServiceClient from DI
                    using var scope = _scopeFactory.CreateScope();
                    var blobServiceClient = scope.ServiceProvider.GetRequiredService<BlobServiceClient>();
                    
                    // Create container and upload file
                    var containerClient = blobServiceClient.GetBlobContainerClient("metricjobs");
                    await containerClient.CreateIfNotExistsAsync();
                    
                    blobName = $"metric_calculation_input/{model.Name}/{clinicalTask.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                    var blobClient = containerClient.GetBlobClient(blobName);
                    
                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));
                    await blobClient.UploadAsync(stream, overwrite: true);
                    
                    _logger.LogInformation($"Uploaded metrics JSON file to {blobName}");
                    return blobName;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not upload metrics JSON to Azure Storage. This may be due to missing configuration.");
                    
                    // Fallback: Save locally for debugging
                    var localPath = Path.Combine(Path.GetTempPath(), $"metrics_{model.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                    await File.WriteAllTextAsync(localPath, jsonContent);
                    _logger.LogInformation($"Saved metrics JSON locally to {localPath}");
                    return "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating metrics JSON file for model {model.Name}");
                return "";
            }
        }

        private string MapEvalMetricToMetricsType(string evalMetric)
        {
            // Map the clinical task evaluation metric to metrics_type
            return evalMetric switch
            {
                "Text-based metrics" => "summarization",
                "Image-based metrics" => "image_quality",
                "Accuracy metrics" => "accuracy",
                "Safety metrics" => "safety",
                "Bias metrics" => "bias",
                _ => "summarization" // Default to summarization if no mapping is found
            };
        }

        private object CreateInstanceFromDataObject(DataObject dataObject, string? prompt, TaskDataSetModel? groundTruthDataSetModel, int id)
        {
            // Find ground truth output using the correct model output index from the groundTruthDataSetModel
            var groundTruth = (groundTruthDataSetModel != null && 
                              dataObject.OutputData.Count > groundTruthDataSetModel.ModelOutputIndex) 
                              ? dataObject.OutputData[groundTruthDataSetModel.ModelOutputIndex] 
                              : null;
            
            // Create input content array with text and images
            var inputContent = new List<object>();
            
            // Add prompt as text
            if (!string.IsNullOrEmpty(prompt))
            {
                inputContent.Add(new
                {
                    type = "Text",
                    data = prompt,
                    location = (string?)null,
                    metadata = (object?)null,
                    highlighted_segments = new object[] { }
                });
            }
            
            // Add all input data
            foreach (var input in dataObject.InputData)
            {
                if (input.Type.StartsWith("imageurl"))
                {
                    // Get base64 image data
                    var (base64Image, mimeType) = GetBase64ImageWithTypeSync(input.Content).Result;
                    
                    inputContent.Add(new
                    {
                        type = "Image",
                        data = $"data:{mimeType};base64,{base64Image}",
                        location = (string?)null,
                        metadata = new
                        {
                            name = $"{Path.GetFileName(input.Content)}",
                            organ = GetOrganFromImageContent(input.Content)
                        },
                        highlighted_segments = new object[] { }
                    });
                }
                else
                {
                    inputContent.Add(new
                    {
                        type = "Text",
                        data = input.Content,
                        location = (string?)null,
                        metadata = (object?)null,
                        highlighted_segments = new object[] { }
                    });
                }
            }
            
            // Create reference array with ground truth
            var references = new List<object>();
            if (groundTruth != null)
            {
                references.Add(new
                {
                    output = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "Text",
                                data = groundTruth.Content,
                                location = (string?)null,
                                metadata = (object?)null,
                                highlighted_segments = new object[] { }
                            }
                        }
                    },
                    tags = new[] { "Correct" }
                });
            }
            
            // Return instance object
            return new
            {
                id,
                input = new
                {
                    content = inputContent
                },
                references,
                split = "Train",
                sub_split = (string?)null,
                perturbation = (string?)null,
            };
        }

        private string GetOrganFromImageContent(string imagePath)
        {
            // Try to determine organ from image path
            // This is a simple example - you may need more sophisticated logic
            if (imagePath.Contains("chest", StringComparison.OrdinalIgnoreCase))
                return "CHEST";
            if (imagePath.Contains("head", StringComparison.OrdinalIgnoreCase))
                return "HEAD";
            if (imagePath.Contains("brain", StringComparison.OrdinalIgnoreCase))
                return "BRAIN";
            if (imagePath.Contains("abdomen", StringComparison.OrdinalIgnoreCase))
                return "ABDOMEN";
            
            return "UNKNOWN";
        }

        // This helper method gets base64 image data synchronously
        private async Task<(string base64, string mimeType)> GetBase64ImageWithTypeSync(string imageId)
        {
            using var scope = _scopeFactory.CreateScope();
            var imageRepo = scope.ServiceProvider.GetRequiredService<IImageRepository>();
            var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();
            
            var image = await imageRepo.GetByIdAsync(imageId);
            var stream = await imageService.GetImageStreamAsync(image);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            
            return (
                Convert.ToBase64String(memoryStream.ToArray()),
                image.ContentType // e.g. "image/jpeg", "image/png"
            );
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

        private (string plainText, List<BoundingBox> boundingBoxes) ParseCXROutput(string rawOutput, string modelId)
        {
            try
            {
                var findings = JsonSerializer.Deserialize<List<List<object>>>(rawOutput);
                if (findings == null) return (rawOutput, new List<BoundingBox>());

                var plainTextParts = new List<string>();
                var boundingBoxes = new List<BoundingBox>();
                
                foreach (var finding in findings)
                {
                    if (finding.Count != 2) continue;
                    
                    var text = finding[0]?.ToString();
                    if (string.IsNullOrEmpty(text)) continue;
                    
                    plainTextParts.Add(text);
                    
                    // Handle coordinates if they exist
                    if (finding[1] != null && finding[1] is JsonElement coordElement && coordElement.ValueKind == JsonValueKind.Array)
                    {
                        try
                        {
                            // Get the inner array of coordinates
                            var coordArray = coordElement.EnumerateArray().FirstOrDefault();
                            if (coordArray.ValueKind == JsonValueKind.Array)
                            {
                                var coords = coordArray.EnumerateArray()
                                    .Select(x => x.GetDouble())
                                    .ToList();

                                if (coords.Count == 4)
                                {
                                    // Store coordinates as percentages (0-1)
                                    var xMin = coords[0];
                                    var yMin = coords[1];
                                    var xMax = coords[2];
                                    var yMax = coords[3];

                                    boundingBoxes.Add(new BoundingBox
                                    {
                                        Id = ObjectId.GenerateNewId().ToString(),
                                        X = xMin,
                                        Y = yMin,
                                        Width = xMax - xMin,
                                        Height = yMax - yMin,
                                        ModelId = modelId,
                                        Annotation = text,
                                        CoordinateType = "percentage"
                                    });
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip this bounding box if parsing fails
                            continue;
                        }
                    }
                }

                return (string.Join(" ", plainTextParts), boundingBoxes);
            }
            catch (JsonException)
            {
                // If parsing fails, return the raw output without bounding boxes
                return (rawOutput, new List<BoundingBox>());
            }
        }
    }
}