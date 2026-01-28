using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using MedBench.Core.Cosmos;
using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Repositories;

public class TestScenarioRepository : ITestScenarioRepository
{
    private readonly Container _container;

    public TestScenarioRepository(CosmosContainerProvider containerProvider)
    {
        _container = containerProvider.GetContainer("TestScenarios");
    }

    public async Task<IEnumerable<TestScenario>> GetAllAsync()
    {
        return await CosmosQueryHelpers.QueryAsync<TestScenario>(
            _container,
            new QueryDefinition("SELECT * FROM c"));
    }

    public async Task<TestScenario> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<TestScenario>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"TestScenario with ID {id} not found");
        }
    }
    public async Task<IEnumerable<TestScenario>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idArray = ids?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (idArray.Length == 0) return Array.Empty<TestScenario>();

        return await CosmosQueryHelpers.QueryAsync<TestScenario>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id)")
                .WithParameter("@ids", idArray));
    }

    public async Task<TestScenario> CreateAsync(TestScenario testScenario)
    {
        if (string.IsNullOrWhiteSpace(testScenario.Id))
        {
            testScenario.Id = Guid.NewGuid().ToString();
        }

        testScenario.CreatedAt = DateTime.UtcNow;
        testScenario.UpdatedAt = DateTime.UtcNow;

        await _container.CreateItemAsync(testScenario, new PartitionKey(testScenario.Id));
        return testScenario;
    }

    public async Task<TestScenario> UpdateAsync(TestScenario testScenario)
    {
        testScenario.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _container.ReplaceItemAsync(testScenario, testScenario.Id, new PartitionKey(testScenario.Id));
            return testScenario;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"TestScenario with ID {testScenario.Id} not found");
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<TestScenario>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"TestScenario with ID {id} not found");
        }
    }

    public async Task<IEnumerable<TestScenario>> GetByClinicalTaskIdsAsync(List<string> taskIds)
    {
        var ids = taskIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (ids.Length == 0) return Array.Empty<TestScenario>();

        return await CosmosQueryHelpers.QueryAsync<TestScenario>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@taskIds, c.TaskId)")
                .WithParameter("@taskIds", ids));
    }
} 