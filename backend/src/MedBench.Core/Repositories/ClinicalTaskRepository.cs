using MedBench.Core.Cosmos;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Repositories;

public class ClinicalTaskRepository : IClinicalTaskRepository
{
    private readonly Container _container;

    public ClinicalTaskRepository(CosmosContainerProvider containerProvider)
    {
        _container = containerProvider.GetContainer("ClinicalTasks");
    }

    public async Task<IEnumerable<ClinicalTask>> GetAllAsync()
    {
        return await CosmosQueryHelpers.QueryAsync<ClinicalTask>(
            _container,
            new QueryDefinition("SELECT * FROM c"));
    }

    public async Task<ClinicalTask> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<ClinicalTask>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"ClinicalTask with ID {id} not found");
        }
    }

    public async Task<IEnumerable<ClinicalTask>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idArray = ids?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (idArray.Length == 0) return Array.Empty<ClinicalTask>();

        return await CosmosQueryHelpers.QueryAsync<ClinicalTask>(
            _container,
            new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id)")
                .WithParameter("@ids", idArray));
    }

    public async Task<ClinicalTask> CreateAsync(ClinicalTask clinicalTask)
    {
        if (string.IsNullOrWhiteSpace(clinicalTask.Id))
        {
            clinicalTask.Id = Guid.NewGuid().ToString();
        }
        clinicalTask.CreatedAt = DateTime.UtcNow;
        clinicalTask.UpdatedAt = DateTime.UtcNow;
        await _container.CreateItemAsync(clinicalTask, new PartitionKey(clinicalTask.Id));
        return clinicalTask;
    }

    public async Task<ClinicalTask> UpdateAsync(ClinicalTask clinicalTask)
    {
        clinicalTask.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _container.ReplaceItemAsync(clinicalTask, clinicalTask.Id, new PartitionKey(clinicalTask.Id));
            return clinicalTask;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"ClinicalTask with ID {clinicalTask.Id} not found");
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<ClinicalTask>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"ClinicalTask with ID {id} not found");
        }
    }
} 