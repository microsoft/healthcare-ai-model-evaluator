using MedBench.Core.Interfaces;
using MedBench.Core.Models;
using Microsoft.Extensions.Logging;
using MedBench.Core.Extensions;
using MongoDB.Bson;
namespace MedBench.Core.Services;

public class StatCalculatorService
{
    private readonly ITrialRepository _trialRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<StatCalculatorService> _logger;
    private readonly IExperimentRepository _experimentRepository;
    private readonly ITestScenarioRepository _testScenarioRepository;
    private readonly IModelRepository _modelRepository;
    private readonly IClinicalTaskRepository _clinicalTaskRepository;
    public StatCalculatorService(
        ITrialRepository trialRepository,
        IUserRepository userRepository,
        ILogger<StatCalculatorService> logger,
        IExperimentRepository experimentRepository,
        ITestScenarioRepository testScenarioRepository,
        IModelRepository modelRepository,
        IClinicalTaskRepository clinicalTaskRepository
        )
    {
        _trialRepository = trialRepository;
        _userRepository = userRepository;
        _logger = logger;
        _experimentRepository = experimentRepository;
        _testScenarioRepository = testScenarioRepository;
        _modelRepository = modelRepository;
        _clinicalTaskRepository = clinicalTaskRepository;
    }

    public async Task CalculateConcordance(string userId, string experimentId, string dataObjectId)
    {
        try
        {
            // Get all trials for this experiment and data object
            var trials = await _trialRepository.GetTrialsByExperimentAndDataObject(experimentId, dataObjectId);
            var completedTrials = trials.Where(t => t.Status == "done").ToList();

            if (completedTrials.Count < 2) return; // Need at least 2 trials to calculate concordance

            // Get the user's trial
            var userTrial = completedTrials.FirstOrDefault(t => t.UserId == userId);
            if (userTrial == null) return;

            // Get other users' trials
            var otherTrials = completedTrials.Where(t => t.UserId != userId).ToList();

            // Calculate concordance
            int agreements = 0;
            foreach (var otherTrial in otherTrials)
            {
                if (TrialsAgree(userTrial, otherTrial))
                {
                    agreements++;
                }
            }

            double concordance = (double)agreements / otherTrials.Count;

            // Update user's stats
            var user = await _userRepository.GetByIdAsync(userId);
            const string concordanceKey = "AverageConcordance";
            
            if (user.Stats.TryGetValue(concordanceKey, out string? existingValue))
            {
                // Calculate running average
                double existingConcordance = double.Parse(existingValue);
                int totalTrials = int.Parse(user.Stats.GetValueOrDefault("TotalTrials", "0"));
                
                double newAverage = ((existingConcordance * totalTrials) + concordance) / (totalTrials + 1);
                user.Stats[concordanceKey] = newAverage.ToString("F2");
                user.Stats["TotalTrials"] = (totalTrials + 1).ToString();
            }
            else
            {
                // First trial
                user.Stats[concordanceKey] = concordance.ToString("F2");
                user.Stats["TotalTrials"] = "1";
            }

            await _userRepository.UpdateAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating concordance for user {UserId}", userId);
        }
    }

    private bool TrialsAgree(Trial trial1, Trial trial2)
    {
        // Simple validation trials
        if (trial1.ExperimentType == "Simple Validation" || trial1.ExperimentType == "Full Validation")
        {
            return trial1.Response.Text == trial2.Response.Text;
        }
        
        // Arena trials
        if (trial1.ExperimentType == "Arena")
        {
            // Check if both users preferred the same model or both thought both models were good/bad
            return trial1.Response.Text == trial2.Response.Text;
        }

        // Simple evaluation trials
        if (trial1.ExperimentType == "Simple Evaluation")
        {
            // Consider ratings within 1 point of each other as agreement
            if (int.TryParse(trial1.Response.Text, out int rating1) && 
                int.TryParse(trial2.Response.Text, out int rating2))
            {
                return Math.Abs(rating1 - rating2) <= 1;
            }
        }

        return false;
    }

    public async Task CalculateModelResults(string modelId, string experimentId)
    {
        try
        {
            var experiment = await _experimentRepository.GetByIdAsync(experimentId);
            var testScenario = await _testScenarioRepository.GetByIdAsync(experiment.TestScenarioId);
            var clinicalTask = await _clinicalTaskRepository.GetByIdAsync(testScenario.TaskId);
            var trials = await _trialRepository.GetByExperimentIdAsync(experimentId);
            var experimentIds = (await _experimentRepository.GetByTestScenarioIdsAsync([testScenario.Id]))
                .Select(e => e.Id)
                .ToList();
            _logger.LogInformation("Experiment IDs: " + experimentIds.ToJson());
            var completedTrials = trials.Where(t => experimentIds.Contains(t.ExperimentId) && t.Status == "done" && t.ModelOutputs.Any(m => m.ModelId == modelId)).ToList();
            _logger.LogInformation("Completed trials: " + completedTrials.Count);
            if (!completedTrials.Any()) return;
            var model = await _modelRepository.GetByIdAsync(modelId);
            var results = clinicalTask.ModelResults.ContainsKey(modelId) ? clinicalTask.ModelResults[modelId] : new ModelExperimentResults();
            var arenaTrials = completedTrials.Where(t => t.ExperimentType == "Arena").ToList();
            var validationTrials = completedTrials.Where(t => t.ExperimentType == "Simple Validation" || t.ExperimentType == "Full Validation").ToList();
            var simpleValidationTrials = completedTrials.Where(t => t.ExperimentType == "Simple Validation").ToList();
            var simpleEvaluationTrials = completedTrials.Where(t => t.ExperimentType == "Simple Evaluation").ToList();
            var singleEvaluationTrials = completedTrials.Where(t => t.ExperimentType == "Single Evaluation").ToList();

            _logger.LogInformation("Arena trials: " + arenaTrials.Count);
            _logger.LogInformation("Validation trials: " + validationTrials.Count);
            _logger.LogInformation("Simple validation trials: " + simpleValidationTrials.Count);
            _logger.LogInformation("Simple evaluation trials: " + simpleEvaluationTrials.Count);
            _logger.LogInformation("Single evaluation trials: " + singleEvaluationTrials.Count);

            if (arenaTrials.Count > 0)
            {
                results.EloScore = CalculateEloScore(arenaTrials, modelId);
            }
            if (simpleEvaluationTrials.Count > 0)
            {
                results.AverageRating = CalculateAverageRating(simpleEvaluationTrials, modelId);
            }
            if (simpleValidationTrials.Count > 0)
            {
                results.CorrectScore = CalculateCorrectScore(simpleValidationTrials, modelId);
            }
            if (validationTrials.Count > 0)
            {
                results.ValidationTime = CalculateValidationTime(validationTrials);
            }
            _logger.LogInformation("Single evaluation trials: " + singleEvaluationTrials.Count);
            if (singleEvaluationTrials.Count > 0)
            {
                results.SingleEvaluationScores = CalculateSingleEvaluationScores(singleEvaluationTrials, modelId);
            }

            // Update model's results for specific metric and 'All'
            var evalMetricString = clinicalTask.EvalMetric;


            model.ExperimentResultsByMetric[evalMetricString] = results;

            // Update 'All' metric
            //This only collects results from the current clinical task, not all experiments
            model.ExperimentResultsByMetric["All"] = CalculateAggregateResults(model.ExperimentResultsByMetric);
            _logger.LogInformation("Model results: " + model.ToBsonDocument().ToJson());
            clinicalTask.ModelResults[modelId] = results;
            _logger.LogInformation("Clinical task results: " + clinicalTask.ModelResults.ToJson());
            await _clinicalTaskRepository.UpdateAsync(clinicalTask);
            await _modelRepository.UpdateAsync(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating model results for model {ModelId}", modelId);
        }
    }
    private Dictionary<string, double> CalculateSingleEvaluationScores(List<Trial> trials, string modelId)
    {
        var scores = new Dictionary<string, double>();
        var counts = new Dictionary<string, int>();

        foreach (var trial in trials)
        {
            foreach (var question in trial.Questions)
            {
                if (question.Response == null || question.Response == "" || question.EvalMetric == null || question.EvalMetric == "") continue;
                if (question.EvalMetric == "Binary Validation")
                {
                    if (question.Response == "yes")
                    {
                        if (scores.ContainsKey(question.EvalMetric))
                        {
                            scores[question.EvalMetric] += 1;
                            counts[question.EvalMetric]++;
                        }
                        else
                        {
                            scores[question.EvalMetric] = 1;
                            counts[question.EvalMetric] = 1;
                        }
                    }
                    else
                    {
                        if (scores.ContainsKey(question.EvalMetric))
                        {
                            counts[question.EvalMetric]++;
                        }
                        else
                        {
                            scores[question.EvalMetric] = 0;
                            counts[question.EvalMetric] = 1;
                        }
                    }
                    counts[question.EvalMetric]++;
                }
                else
                {
                    try
                    {
                        if (scores.ContainsKey(question.EvalMetric))
                        {
                            scores[question.EvalMetric] += double.Parse(question.Response);
                            counts[question.EvalMetric]++;
                        }
                        else
                        {
                            scores[question.EvalMetric] = double.Parse(question.Response);
                            counts[question.EvalMetric] = 1;
                        }
                    } catch (FormatException)
                    {
                        _logger.LogWarning("Failed to parse response for question: {QuestionText} with response: {Response}", question.QuestionText, question.Response);
                        // If parsing fails, we can skip this question or handle it as needed
                        continue;
                    }
                    
                }
            }
        }
        // Calculate averages
        foreach (var key in scores.Keys.ToList())
        {
            scores[key] /= counts[key];
        }
        _logger.LogInformation("Single evaluation scores: " + scores.ToJson());

        return scores;
    }
    private double CalculateEloScore(List<Trial> trials, string modelId)
    {
        const int K = 32; // ELO K-factor
        double eloScore = 1500; // Starting score

        foreach (var trial in trials)
        {
            if (trial.Response?.Text == null) continue;

            var response = trial.Response.Text.ToUpperInvariant();
            var isWin = response == "A" && trial.ModelOutputs[0].ModelId == modelId ||
                       response == "B" && trial.ModelOutputs[1].ModelId == modelId;
            var isDraw = response == "BOTH-GOOD" || response == "BOTH-BAD";

            if (isWin)
                eloScore += K;
            else if (!isDraw)
                eloScore -= K;
        }

        return eloScore;
    }

    private double CalculateAverageRating(List<Trial> trials, string modelId)
    {
        var ratings = trials
            .Where(t => int.TryParse(t.Response?.Text, out _))
            .Select(t => int.Parse(t.Response!.Text));

        return ratings.Any() ? ratings.Average() : 0;
    }

    private double CalculateCorrectScore(List<Trial> trials, string modelId)
    {
        var validationTrials = trials.Where(t => t.Response?.Text != null);
        if (!validationTrials.Any()) return 0;

        var correctCount = validationTrials.Count(t => t.Response!.Text.ToLower() == "yes");
        return (double)correctCount / validationTrials.Count() * 100;
    }

    private double CalculateValidationTime(List<Trial> trials)
    {
        return trials.Any() ? trials.Average(t => t.TotalTime) : 0;
    }

    private Dictionary<string, double> AggregateSingleEvaluationScores(List<Dictionary<string, double>> scoresList)
    {
        var aggregatedScores = new Dictionary<string, double>();
        var counts = new Dictionary<string, int>();
        foreach (var scores in scoresList)
        {
            foreach (var kvp in scores)
            {
                if (aggregatedScores.ContainsKey(kvp.Key))
                {
                    aggregatedScores[kvp.Key] += kvp.Value;
                    counts[kvp.Key]++;
                }
                else
                {
                    aggregatedScores[kvp.Key] = kvp.Value;
                    counts[kvp.Key] = 1;
                }
            }
        }

        // Calculate averages
        foreach (var key in aggregatedScores.Keys.ToList())
        {
            aggregatedScores[key] /= counts[key];
        }

        return aggregatedScores;
    }

    private ModelExperimentResults CalculateAggregateResults(Dictionary<string, ModelExperimentResults> resultsByMetric)
    {
        var metrics = resultsByMetric.Values.ToList();
        if (!metrics.Any()) return new ModelExperimentResults();


        return new ModelExperimentResults
        {
            EloScore = metrics.Average(m => m.EloScore),
            AverageRating = metrics.Average(m => m.AverageRating),
            CorrectScore = metrics.Average(m => m.CorrectScore),
            ValidationTime = metrics.Average(m => m.ValidationTime),
            SingleEvaluationScores = AggregateSingleEvaluationScores(metrics.Select(m => m.SingleEvaluationScores).ToList())
        };
    }
}
