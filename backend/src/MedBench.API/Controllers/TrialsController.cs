using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Services;

namespace MedBench.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrialsController : ControllerBase
    {
        private readonly ITrialRepository _trialRepository;
        private readonly IExperimentRepository _experimentRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<TrialsController> _logger;
        private readonly StatCalculatorService _statCalculatorService;
        private readonly ITestScenarioRepository _testScenarioRepository;
        

        public TrialsController(
            ITrialRepository trialRepository,
            IExperimentRepository experimentRepository,
            IUserRepository userRepository,
            ILogger<TrialsController> logger,
            StatCalculatorService statCalculatorService,
            ITestScenarioRepository testScenarioRepository)
        {
            _trialRepository = trialRepository;
            _experimentRepository = experimentRepository;
            _userRepository = userRepository;
            _logger = logger;
            _statCalculatorService = statCalculatorService;
            _testScenarioRepository = testScenarioRepository;
        }

        [HttpGet("pending-counts")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<ActionResult<Dictionary<string, int>>> GetPendingTrialCounts()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var validStatuses = new[] { "pending", "skipped" };
                var validExperimentStatuses = new[] { "InProgress" };
                var counts = await _trialRepository.GetPendingTrialCountsByType(userId, validStatuses, validExperimentStatuses);
                
                return Ok(counts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending trial counts");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("next-pending")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<ActionResult<Trial>> GetNextPendingTrial([FromQuery] string testScenarioIds = "")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();
                    
                var scenarioIds = string.IsNullOrEmpty(testScenarioIds) 
                    ? new List<string>() 
                    : testScenarioIds.Split(',').ToList();
                
                System.Console.WriteLine("GetNextPendingTrial  UserId: " + userId);
                System.Console.WriteLine("GetNextPendingTrial  TestScenarioIds: " + string.Join(",", scenarioIds));

                // Filter by test scenario IDs if provided
                if (scenarioIds.Any())
                {
                    // Get experiments for those test scenarios that are InProgress
                    var experiments = await _experimentRepository.GetByTestScenarioIdsAsync(scenarioIds);
                    var inProgressExperiments = experiments.Where(e => e.Status == ExperimentStatus.InProgress).ToList();
                    var experimentIds = inProgressExperiments.Select(e => e.Id).ToList();
                    System.Console.WriteLine("Found Total Experiments Count: " + experiments.Count());
                    System.Console.WriteLine("Found InProgress Experiments Count: " + inProgressExperiments.Count());
                    System.Console.WriteLine("Experiment IDs (InProgress): " + string.Join(",", experimentIds));
                    System.Console.WriteLine("All Experiment Statuses: " + string.Join(", ", experiments.Select(e => $"{e.Id}:{e.Status}")));
                    if (!experimentIds.Any())
                    {
                        return NotFound(new { message = "No in-progress experiments found for the provided test scenario IDs." });
                    }
                    // Filter trials by those experiment IDs
                    foreach (Experiment experiment in experiments)
                    {
                        var trialids = await _trialRepository.GetPendingTrialIdsAsync(userId, experimentIds: [experiment.Id]);
                        System.Console.WriteLine("Filtered Trials Count After: " + trialids.Count());
                        var trialid = trialids.FirstOrDefault();
                        // Debug: Show trial statuses
                        if (experiment.Randomized)
                        {
                            var randomTrialId = trialids
                            .OrderBy(t => Guid.NewGuid()) // Randomly order
                            .FirstOrDefault(); // Get the first one    
                            trialid = randomTrialId;
                        }
                        if (trialid != null)
                        {
                           var trial = await _trialRepository.GetByIdAsync(trialid);
                            trial.StartedOn = DateTime.UtcNow;
                            _ = _trialRepository.UpdateAsync(trial);

                            return Ok(trial);
                        }
                        
                    }
                    return NotFound(new { message = "No pending trials found." });

                }
                else
                {
                    return NotFound(new { message = "No pending trials available." });
                }
                
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next pending trial");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("next-done")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<ActionResult<Trial>> GetNextDoneTrial([FromQuery] string testScenarioIds = "", [FromQuery] string afterTrialId = "")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();
                    
                var scenarioIds = string.IsNullOrEmpty(testScenarioIds) 
                    ? new List<string>() 
                    : testScenarioIds.Split(',').ToList();

                System.Console.WriteLine("GetNextDoneTrial  UserId: " + userId);
                System.Console.WriteLine("GetNextDoneTrial  TestScenarioIds: " + string.Join(",", scenarioIds));

                // Filter by test scenario IDs if provided
                if (scenarioIds.Any())
                {
                    // Get experiments for those test scenarios that are InProgress
                    var experiments = await _experimentRepository.GetByTestScenarioIdsAsync(scenarioIds);
                    var inProgressExperiments = experiments.Where(e => e.Status == ExperimentStatus.InProgress).ToList();
                    var experimentIds = inProgressExperiments.Select(e => e.Id).ToList();
                    if (!experimentIds.Any())
                    {
                        return NotFound(new { message = "No in-progress experiments found for the provided test scenario IDs." });
                    }

                    var trialids = await _trialRepository.GetDoneTrialIdsAsync(userId, experimentIds: experimentIds.ToArray());
                    var trialidsList = trialids.ToList();
                    var indexOfNextTrial = 0;

                    if (trialidsList.Count() == 0)
                    {
                        return NotFound(new { message = "No done trials found for the provided test scenario IDs." });
                    }
                    if (trialidsList.Contains(afterTrialId))
                    {
                        var indexOfLastTrial = string.IsNullOrEmpty(afterTrialId) ? -1 : trialidsList.IndexOf(afterTrialId);
                        indexOfNextTrial = indexOfLastTrial + 1;
                    }
                    string? trialid;
                    if (string.IsNullOrEmpty(afterTrialId))
                    {
                        // No afterTrialId provided, return first trial
                        trialid = trialidsList.FirstOrDefault();
                    }
                    else if (indexOfNextTrial >= trialidsList.Count)
                    {
                        // Already at the last trial, circle back to first
                        trialid = trialidsList.FirstOrDefault();
                    }
                    else
                    {
                        // Get the next trial in sequence
                        trialid = trialidsList[indexOfNextTrial];
                    }
                    if (trialid != null)
                    {
                        var trial = await _trialRepository.GetByIdAsync(trialid);
                        trial.StartedOn = DateTime.UtcNow;
                        _ = _trialRepository.UpdateAsync(trial);

                        return Ok(trial);
                    }
                        
                    return NotFound(new { message = "No pending trials found." });

                }
                else
                {
                    return NotFound(new { message = "No pending trials available." });
                }
                
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next pending trial");
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpGet("get-done-ids")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<ActionResult<IEnumerable<string>>> GetDoneTrialIds([FromQuery] string testScenarioIds = "")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();
                var scenarioIds = string.IsNullOrEmpty(testScenarioIds)
                    ? new List<string>()
                    : testScenarioIds.Split(',').ToList();
                // Filter by test scenario IDs if provided
                if (scenarioIds.Any())
                {
                    // Get experiments for those test scenarios that are InProgress
                    var experiments = await _experimentRepository.GetByTestScenarioIdsAsync(scenarioIds);
                    var inProgressExperiments = experiments.Where(e => e.Status == ExperimentStatus.InProgress).ToList();
                    var experimentIds = inProgressExperiments.Select(e => e.Id).ToList();
                    if (!experimentIds.Any())
                    {
                        return Ok(new List<string>());
                    }
                    var trialids = await _trialRepository.GetDoneTrialIdsAsync(userId, experimentIds: experimentIds.ToArray());
                    return Ok(trialids);

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting done trial IDs");
                return StatusCode(500, "Internal server error");
            }
            return Ok(new List<string>());
        }
        
        [HttpGet("get-done")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<ActionResult<Trial>> GetDoneTrial([FromQuery] string trialId = "")
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();
                    
                var trial = await _trialRepository.GetByIdAsync(trialId);
                if (trial == null || trial.UserId != userId || trial.Status != "done")
                {
                    return NotFound(new { message = "No done trial found with the provided ID." });
                }
                return Ok(trial);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next pending trial");
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpPut("{id}")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<IActionResult> UpdateTrial(string id, [FromBody] TrialUpdateDto update)
        {
            try
            {
                var existingTrial = await _trialRepository.GetByIdAsync(id);
                if (existingTrial == null)
                    return NotFound();

                var wasNotCompleted = existingTrial.Status == "pending" || existingTrial.Status == "skipped";
                var statusProvided = update.Status != null;
                var isNowCompleted = statusProvided && update.Status == "done";
                _logger.LogInformation($"Received trial update payload: {System.Text.Json.JsonSerializer.Serialize(update)}");

                // Update trial
                if (statusProvided)
                    existingTrial.Status = update.Status!;
                if (update.Response != null)
                    existingTrial.Response = update.Response;
                if (update.Flags != null)
                    existingTrial.Flags = update.Flags;
                if (update.Questions != null)
                    existingTrial.Questions = update.Questions;
                if (update.BoundingBoxes != null)
                    existingTrial.BoundingBoxes = update.BoundingBoxes;

                 var now = DateTime.UtcNow;
        
                // Calculate new time spent
                var newTimeSpent = (now - existingTrial.StartedOn).TotalSeconds;
                
                // Subtract previous time period if it exists
                var previousTimeSpent = (existingTrial.UpdatedAt - existingTrial.StartedOn).TotalSeconds;
                if (previousTimeSpent > 0)
                {
                    existingTrial.TotalTime = existingTrial.TotalTime - previousTimeSpent + newTimeSpent;
                }
                else
                {
                    existingTrial.TotalTime += newTimeSpent;
                }

                existingTrial.UpdatedAt = now;
                // Update user's total time stats
                var user = await _userRepository.GetByIdAsync(existingTrial.UserId);
                var statsKey = "TotalTrialTimeSeconds";
                var currentTotal = user.Stats.ContainsKey(statsKey) ? 
                    double.Parse(user.Stats[statsKey]) : 0;
                
                user.Stats[statsKey] = (currentTotal + existingTrial.TotalTime)
                    .ToString("F2");
               
                var updatedTrial = await _trialRepository.UpdateAsync(existingTrial);
                if (isNowCompleted)
                {
                    // Existing concordance calculation
                    await _statCalculatorService.CalculateConcordance(
                        existingTrial.UserId,
                        existingTrial.ExperimentId,
                        existingTrial.DataObjectId ?? string.Empty
                    );

                    // Calculate model results for each model in the trial
                    foreach (var output in (existingTrial.ModelOutputs ?? new List<ModelOutput>()))
                    {
                        await _statCalculatorService.CalculateModelResults(
                            output.ModelId,
                            existingTrial.ExperimentId
                        );
                    }
                }
                // Update experiment pending count if status changed
                if (wasNotCompleted && isNowCompleted)
                {
                    var experiment = await _experimentRepository.GetByIdAsync(existingTrial.ExperimentId);
                    if (experiment.PendingTrials.HasValue)
                    {
                        experiment.PendingTrials--;
                        await _experimentRepository.UpdateAsync(experiment);
                    }
                }
                await _userRepository.UpdateAsync(user);
                // If user just skipped a trial, check if there are any pending trials left
                if (statusProvided && update.Status == "skipped" && existingTrial.TestScenarioId != null)
                {
                    var pendingTrialCount = await _trialRepository.GetPendingTrialCountForTestScenarioAsync(
                        existingTrial.UserId, 
                        existingTrial.TestScenarioId);

                    // If no pending trials remain, convert all skipped trials back to pending
                    if (pendingTrialCount == 0 && existingTrial.TestScenarioId != null)
                    {
                        var skippedTrials = await _trialRepository.UnskipTrialsAsync(
                            existingTrial.UserId,  
                            existingTrial.TestScenarioId);
                        _logger.LogInformation($"Unskipped {skippedTrials.Count()} trials for user {existingTrial.UserId} in test scenario {existingTrial.TestScenarioId}");
                    }
                }
                return Ok(updatedTrial);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trial");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("flags/{id}")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<IActionResult> UpdateTrialFlags(string id, [FromBody] Trial trial)
        {
            try
            {
                var existingTrial = await _trialRepository.GetByIdAsync(id);
                if (existingTrial == null)
                    return NotFound();

              

                // Update trial
                existingTrial.Flags = trial.Flags;
                
                 var now = DateTime.UtcNow;
        
                // Calculate new time spent
                var newTimeSpent = (now - existingTrial.StartedOn).TotalSeconds;
                
                // Subtract previous time period if it exists
                var previousTimeSpent = (existingTrial.UpdatedAt - existingTrial.StartedOn).TotalSeconds;
                if (previousTimeSpent > 0)
                {
                    existingTrial.TotalTime = existingTrial.TotalTime - previousTimeSpent + newTimeSpent;
                }
                else
                {
                    existingTrial.TotalTime += newTimeSpent;
                }

                existingTrial.UpdatedAt = now;
                // Update user's total time stats
                var user = await _userRepository.GetByIdAsync(existingTrial.UserId);
                var statsKey = "TotalTrialTimeSeconds";
                var currentTotal = user.Stats.ContainsKey(statsKey) ? 
                    double.Parse(user.Stats[statsKey]) : 0;
                
                user.Stats[statsKey] = (currentTotal + existingTrial.TotalTime)
                    .ToString("F2");
               
                var updatedTrial = await _trialRepository.UpdateAsync(existingTrial);
                
                await _userRepository.UpdateAsync(user);

                return Ok(updatedTrial);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trial");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("stats")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<ActionResult<IEnumerable<TrialStats>>> GetTrialStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var trials = await _trialRepository.GetByUserIdAsync(userId);
                Console.WriteLine("GetTrialStats  UserId: " + userId);
                var user = await _userRepository.GetByIdAsync(userId);
                var experimentIds = trials.Select(t => t.ExperimentId).Distinct().ToList();
                var experiments = await _experimentRepository.GetByIdsAsync(experimentIds);
                var testScenariosIds = experiments.Select(e => e.TestScenarioId).Distinct().ToList();
                var testScenarios = await _testScenarioRepository.GetByIdsAsync(testScenariosIds);
                
                // Group trials by test scenario using experiment mapping
                var stats = new List<TrialStats>();
                foreach (var testScenario in testScenarios)
                {
                    var experimentsForScenario = experiments.Where(e => e.TestScenarioId == testScenario.Id).ToList();
                    var experimentIdsForScenario = experimentsForScenario.Select(e => e.Id).ToList();
                    
                    var trialsForScenario = trials
                        .Where(t => experimentIdsForScenario.Contains(t.ExperimentId))
                        .ToList();

                    if (!trialsForScenario.Any()) continue;

                    var completedTrials = trialsForScenario.Where(t => t.Status == "done").ToList();
                    var totalTimeSeconds = completedTrials.Sum(t => t.TotalTime);
                    var averageConcordance = CalculateAverageConcordance(completedTrials);
                    
                    stats.Add(new TrialStats
                    {
                        ExperimentIds = experimentIdsForScenario,
                        TestScenarioId = testScenario.Id,
                        TotalTimeSeconds = totalTimeSeconds,
                        AverageConcordance = averageConcordance,
                        PendingCount = trialsForScenario.Count(t => t.ExperimentStatus== "InProgress" && (t.Status == "pending" || t.Status == "skipped")),
                        CompletedCount = completedTrials.Count,
                    });
                }

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trial stats");
                return StatusCode(500, "Internal server error");
            }
        }

        private double CalculateAverageConcordance(IEnumerable<Trial> trials)
        {
            var completedTrials = trials.Where(t => t.Status == "done" && !string.IsNullOrEmpty(t.Response?.Text)).ToList();
            if (!completedTrials.Any()) return 0;

            // For now, return a placeholder value since the actual concordance calculation would depend on your specific requirements
            return 80.0; // Example value
        }

        private double CalculateTotalTimeHours(IEnumerable<Trial> trials)
        {
            var completedTrials = trials.Where(t => t.Status == "done").ToList();
            if (!completedTrials.Any()) return 0;

            var totalMinutes = completedTrials.Sum(t => t.TotalTime);
            return Math.Round(totalMinutes / 60.0, 1);
        }

        [HttpGet("experiment/{experimentId}")]
        [Authorize(Policy = "RequireAuthenticatedUser")]
        public async Task<ActionResult<IEnumerable<Trial>>> GetTrialsByExperiment(string experimentId)
        {
            var trials = await _trialRepository.GetByExperimentIdAsync(experimentId);
            return Ok(trials);
        }
    }
} 