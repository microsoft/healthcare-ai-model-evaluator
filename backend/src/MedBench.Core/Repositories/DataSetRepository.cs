using MedBench.Core.Cosmos;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Repositories;

public class DataSetRepository : IDataSetRepository
{
    private readonly Container _container;
    private readonly IDataObjectRepository _dataObjectRepository;

    public DataSetRepository(CosmosContainerProvider containerProvider, IDataObjectRepository dataObjectRepository)
    {
        _container = containerProvider.GetContainer("DataSets");
        _dataObjectRepository = dataObjectRepository;
    }

    public async Task<IEnumerable<DataSet>> GetAllAsync()
    {
        return await CosmosQueryHelpers.QueryAsync<DataSet>(
            _container,
            new QueryDefinition("SELECT * FROM c"));
    }

    public async Task<DataSet> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<DataSet>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"DataSet with ID {id} not found");
        }
    }

    public async Task<DataSet> CreateAsync(DataSet dataset)
    {
        if (string.IsNullOrWhiteSpace(dataset.Id))
        {
            dataset.Id = Guid.NewGuid().ToString();
        }

        await _container.CreateItemAsync(dataset, new PartitionKey(dataset.Id));
        return dataset;
    }

    public async Task<DataSet> UpdateAsync(DataSet dataset)
    {
        try
        {
            await _container.ReplaceItemAsync(dataset, dataset.Id, new PartitionKey(dataset.Id));
            return dataset;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"DataSet with ID {dataset.Id} not found");
        }
    }

    public async Task DeleteAsync(string id)
    {
        await _dataObjectRepository.DeleteByDataSetIdAsync(id);

        try
        {
            await _container.DeleteItemAsync<DataSet>(id, new PartitionKey(id));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"DataSet with ID {id} not found");
        }
    }
} 