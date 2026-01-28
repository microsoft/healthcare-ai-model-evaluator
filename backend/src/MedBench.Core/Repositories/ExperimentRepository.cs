using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Cosmos;
using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Repositories;

public class ExperimentRepository : IExperimentRepository
{
    private readonly Container _container;

    public ExperimentRepository(CosmosContainerProvider containerProvider)
    {
        _container = containerProvider.GetContainer("Experiments");
    }

    public async Task<IEnumerable<Experiment>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idArray = ids?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (idArray.Length == 0) return Array.Empty<Experiment>();

        return await CosmosQueryHelpers.QueryAsync<Experiment>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id)")
                .WithParameter("@ids", idArray));
    }

    public async Task<IEnumerable<Experiment>> GetAllAsync()
    {
        return await CosmosQueryHelpers.QueryAsync<Experiment>(
            _container,
            new QueryDefinition("SELECT * FROM c"));
    }

    public async Task<Experiment> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Experiment>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Experiment with ID {id} not found");
        }
    }

    public async Task<Experiment> CreateAsync(Experiment experiment)
    {
        if (string.IsNullOrWhiteSpace(experiment.Id))
        {
            experiment.Id = Guid.NewGuid().ToString();
        }

        if (experiment.CreatedAt == default)
        {
            experiment.CreatedAt = DateTime.UtcNow;
        }

        experiment.UpdatedAt = DateTime.UtcNow;

        await _container.CreateItemAsync(experiment, new PartitionKey(experiment.Id));
        return experiment;
    }

    public async Task<Experiment> UpdateAsync(Experiment experiment)
    {
        experiment.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _container.ReplaceItemAsync(experiment, experiment.Id, new PartitionKey(experiment.Id));
            return experiment;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Experiment with ID {experiment.Id} not found");
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<Experiment>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Experiment with ID {id} not found");
        }
    }

    public async Task<IEnumerable<Experiment>> GetByUserIdAsync(string userId)
    {
        return await CosmosQueryHelpers.QueryAsync<Experiment>(
            _container,
            new QueryDefinition(
                    "SELECT * FROM c " +
                    "WHERE c.OwnerId = @userId " +
                    "OR ARRAY_CONTAINS(c.ReviewerIds, @userId) " +
                    "OR ARRAY_CONTAINS(c.AssignedUserIds, @userId)")
                .WithParameter("@userId", userId));
    }

    public async Task<IEnumerable<Experiment>> GetByProcessingStatusAsync(ProcessingStatus status)
    {
        return await CosmosQueryHelpers.QueryAsync<Experiment>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE c.ProcessingStatus = @status")
                .WithParameter("@status", status.ToString()));
    }

    public async Task<IEnumerable<Experiment>> GetByTestScenarioIdsAsync(List<string> scenarioIds)
    {
        var ids = scenarioIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (ids.Length == 0) return Array.Empty<Experiment>();

        return await CosmosQueryHelpers.QueryAsync<Experiment>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@scenarioIds, c.TestScenarioId)")
                .WithParameter("@scenarioIds", ids));
    }
}