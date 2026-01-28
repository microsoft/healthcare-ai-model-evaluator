using System.Threading.Tasks;
using System.Text.Json;
using MedBench.Core.Cosmos;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Repositories;

public class TrialRepository : ITrialRepository
{
    private readonly Container _container;
    private readonly ILogger<TrialRepository> _logger;

    public TrialRepository(CosmosContainerProvider containerProvider, ILogger<TrialRepository> logger)
    {
        _container = containerProvider.GetContainer("Trials");
        _logger = logger;
    }

    public async Task<IEnumerable<Trial>> GetAllAsync()
    {
        return await CosmosQueryHelpers.QueryAsync<Trial>(
            _container,
            new QueryDefinition("SELECT * FROM c"));
    }

    public async Task<Trial> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Trial>(id, new PartitionKey(id));
            var trial = response.Resource;
            _logger.LogInformation("Retrieved trial from DB: {Trial}", JsonSerializer.Serialize(trial));
            return trial;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Trial with ID {id} not found");
        }
    }

    public async Task<IEnumerable<Trial>> GetByUserIdAsync(string userId)
    {
        return await CosmosQueryHelpers.QueryAsync<Trial>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId")
                .WithParameter("@userId", userId));
    }

    public async Task<IEnumerable<Trial>> GetByExperimentIdAsync(string experimentId)
    {
        return await CosmosQueryHelpers.QueryAsync<Trial>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE c.ExperimentId = @experimentId")
                .WithParameter("@experimentId", experimentId));
    }

    public async Task<Trial> CreateAsync(Trial trial)
    {
        trial.StartedOn = DateTime.UtcNow;
        trial.CreatedAt = DateTime.UtcNow;
        trial.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(trial.Id))
        {
            trial.Id = Guid.NewGuid().ToString();
        }
        await _container.CreateItemAsync(trial, new PartitionKey(trial.Id));
        return trial;
    }

    public async Task<Trial> UpdateAsync(Trial trial)
    {
        trial.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _container.ReplaceItemAsync(trial, trial.Id, new PartitionKey(trial.Id));
            return trial;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Trial with ID {trial.Id} not found");
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<Trial>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Trial with ID {id} not found");
        }
    }

    public async Task DeleteByExperimentIdAsync(string experimentId)
    {
        var idsToDelete = await CosmosQueryHelpers.QueryAsync<string>(
            _container,
            new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.ExperimentId = @experimentId")
                .WithParameter("@experimentId", experimentId));

        foreach (var id in idsToDelete)
        {
            await _container.DeleteItemAsync<Trial>(id, new PartitionKey(id));
        }

        if (idsToDelete.Count > 0)
        {
            _logger.LogInformation("Deleted {Count} trials for experiment {ExperimentId}", idsToDelete.Count, experimentId);
        }
    }

    public async Task UpdateExperimentStatusAsync(string experimentId, string status)
    {
        var idsToUpdate = await CosmosQueryHelpers.QueryAsync<string>(
            _container,
            new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.ExperimentId = @experimentId AND c.ExperimentStatus != @status")
                .WithParameter("@experimentId", experimentId)
                .WithParameter("@status", status));

        foreach (var id in idsToUpdate)
        {
            await _container.PatchItemAsync<Trial>(
                id,
                new PartitionKey(id),
                new[] { PatchOperation.Set("/ExperimentStatus", status), PatchOperation.Set("/UpdatedAt", DateTime.UtcNow) });
        }

        if (idsToUpdate.Count > 0)
        {
            _logger.LogInformation("Updated {Count} trials for experiment {ExperimentId} to status {Status}", idsToUpdate.Count, experimentId, status);
        }
    }

    public async Task<IEnumerable<Trial>> GetPendingTrialsAsync(string userId)
    {
        var trials = await CosmosQueryHelpers.QueryAsync<Trial>(
            _container,
            new QueryDefinition(
                    "SELECT * FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND c.Status = 'pending' " +
                    "AND c.ExperimentStatus = 'InProgress'")
                .WithParameter("@userId", userId));
        if (trials.Count > 0)
        {
            return trials;
        }

        var skippedTrials = await CosmosQueryHelpers.QueryAsync<Trial>(
            _container,
            new QueryDefinition(
                    "SELECT * FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND c.Status = 'skipped' " +
                    "AND c.ExperimentStatus = 'InProgress'")
                .WithParameter("@userId", userId));
        if (skippedTrials.Count > 0)
        {
            return skippedTrials;
        }
        // If no matching trials, return empty
        return new List<Trial>();
    }
    public async Task<IEnumerable<string>> GetDoneTrialIdsAsync(string userId, string[] experimentIds)
    {
        var ids = experimentIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (ids.Length == 0) return Array.Empty<string>();

        return await CosmosQueryHelpers.QueryAsync<string>(
            _container,
            new QueryDefinition(
                    "SELECT VALUE c.id FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND ARRAY_CONTAINS(@experimentIds, c.ExperimentId) " +
                    "AND c.Status = 'done' " +
                    "AND c.ExperimentStatus = 'InProgress'")
                .WithParameter("@userId", userId)
                .WithParameter("@experimentIds", ids));
    }
    
    public async Task<IEnumerable<string>> UnskipTrialsAsync(string userId, string testScenarioId)
    {
        var trialIds = await CosmosQueryHelpers.QueryAsync<string>(
            _container,
            new QueryDefinition(
                    "SELECT VALUE c.id FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND c.TestScenarioId = @testScenarioId " +
                    "AND c.Status = 'skipped' " +
                    "AND c.ExperimentStatus = 'InProgress'")
                .WithParameter("@userId", userId)
                .WithParameter("@testScenarioId", testScenarioId));

        foreach (var id in trialIds)
        {
            await _container.PatchItemAsync<Trial>(
                id,
                new PartitionKey(id),
                new[] { PatchOperation.Set("/Status", "pending"), PatchOperation.Set("/UpdatedAt", DateTime.UtcNow) });
        }

        if (trialIds.Count > 0)
        {
            _logger.LogInformation("Unskipped {Count} trials for user {UserId} in test scenario {TestScenarioId}", trialIds.Count, userId, testScenarioId);
        }

        return trialIds;
    }

    public async Task<int> GetPendingTrialCountForTestScenarioAsync(string userId, string testScenarioId)
    {
        return await CosmosQueryHelpers.QueryScalarAsync<int>(
            _container,
            new QueryDefinition(
                    "SELECT VALUE COUNT(1) FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND c.TestScenarioId = @testScenarioId " +
                    "AND c.Status = 'pending' " +
                    "AND c.ExperimentStatus = 'InProgress'")
                .WithParameter("@userId", userId)
                .WithParameter("@testScenarioId", testScenarioId));
    }
    public async Task<IEnumerable<string>> GetPendingTrialIdsAsync(string userId, string[]? experimentIds, string? testScenarioId)
    {
        if (experimentIds == null && testScenarioId == null)
        {
            throw new ArgumentException("Either experimentIds or testScenarioId must be provided");
        }
        if (experimentIds == null)
        {
            experimentIds = Array.Empty<string>();
        }
        if (testScenarioId != null)
        {
            var trialIdsByTestScenario = await CosmosQueryHelpers.QueryAsync<string>(
                _container,
                new QueryDefinition(
                        "SELECT VALUE c.id FROM c " +
                        "WHERE c.UserId = @userId " +
                        "AND c.TestScenarioId = @testScenarioId " +
                        "AND c.Status = 'pending' " +
                        "AND c.ExperimentStatus = 'InProgress'")
                    .WithParameter("@userId", userId)
                    .WithParameter("@testScenarioId", testScenarioId));
            if (trialIdsByTestScenario.Count > 0)
            {
                return trialIdsByTestScenario;
            }
            return await CosmosQueryHelpers.QueryAsync<string>(
                _container,
                new QueryDefinition(
                        "SELECT VALUE c.id FROM c " +
                        "WHERE c.UserId = @userId " +
                        "AND c.TestScenarioId = @testScenarioId " +
                        "AND c.Status = 'skipped' " +
                        "AND c.ExperimentStatus = 'InProgress'")
                    .WithParameter("@userId", userId)
                    .WithParameter("@testScenarioId", testScenarioId));
        }
        var expIds = experimentIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (expIds.Length == 0) return Array.Empty<string>();

        var trialIds = await CosmosQueryHelpers.QueryAsync<string>(
            _container,
            new QueryDefinition(
                    "SELECT VALUE c.id FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND ARRAY_CONTAINS(@experimentIds, c.ExperimentId) " +
                    "AND c.Status = 'pending' " +
                    "AND c.ExperimentStatus = 'InProgress'")
                .WithParameter("@userId", userId)
                .WithParameter("@experimentIds", expIds));
        if (trialIds.Count > 0)
        {
            return trialIds;
        }
        return await CosmosQueryHelpers.QueryAsync<string>(
            _container,
            new QueryDefinition(
                    "SELECT VALUE c.id FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND ARRAY_CONTAINS(@experimentIds, c.ExperimentId) " +
                    "AND c.Status = 'skipped' " +
                    "AND c.ExperimentStatus = 'InProgress'")
                .WithParameter("@userId", userId)
                .WithParameter("@experimentIds", expIds));
    }

    public async Task<Dictionary<string, int>> GetPendingTrialCountsByType(string userId, string[] validStatuses, string[] validExperimentStatuses)
    {
        var trials = await CosmosQueryHelpers.QueryAsync<TrialExperimentTypeOnly>(
            _container,
            new QueryDefinition(
                    "SELECT c.ExperimentType FROM c " +
                    "WHERE c.UserId = @userId " +
                    "AND ARRAY_CONTAINS(@validStatuses, c.Status) " +
                    "AND ARRAY_CONTAINS(@validExperimentStatuses, c.ExperimentStatus)")
                .WithParameter("@userId", userId)
                .WithParameter("@validStatuses", validStatuses)
                .WithParameter("@validExperimentStatuses", validExperimentStatuses));

        var counts = new Dictionary<string, int>
        {
            { "Simple Evaluation", 0 },
            { "Simple Validation", 0 },
            { "Arena", 0 },
            { "Full Validation", 0 }
        };

        foreach (var trial in trials)
        {
            if (counts.ContainsKey(trial.ExperimentType))
            {
                counts[trial.ExperimentType]++;
            }
        }

        return counts;
    }

    public async Task<IEnumerable<Trial>> GetTrialsByExperimentAndDataObject(string experimentId, string dataObjectId)
    {
        return await CosmosQueryHelpers.QueryAsync<Trial>(
            _container,
            new QueryDefinition(
                    "SELECT * FROM c " +
                    "WHERE c.ExperimentId = @experimentId " +
                    "AND c.DataObjectId = @dataObjectId")
                .WithParameter("@experimentId", experimentId)
                .WithParameter("@dataObjectId", dataObjectId));
    }

    public async Task<int> GetPendingTrialCountForExperiment(string experimentId)
    {
        return await CosmosQueryHelpers.QueryScalarAsync<int>(
            _container,
            new QueryDefinition(
                    "SELECT VALUE COUNT(1) FROM c " +
                    "WHERE c.ExperimentId = @experimentId " +
                    "AND c.Status = 'pending'")
                .WithParameter("@experimentId", experimentId));
    }

    private sealed class TrialExperimentTypeOnly
    {
        public string ExperimentType { get; set; } = string.Empty;
    }
} 