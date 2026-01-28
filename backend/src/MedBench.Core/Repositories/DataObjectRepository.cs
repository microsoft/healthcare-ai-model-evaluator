using MedBench.Core.Cosmos;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using Microsoft.Azure.Cosmos;

namespace MedBench.Core.Repositories;

public class DataObjectRepository : IDataObjectRepository
{
    private readonly Container _dataObjects;
    private readonly Container _dataSets;

    internal DataObjectRepository(Container dataObjects, Container dataSets)
    {
        _dataObjects = dataObjects;
        _dataSets = dataSets;
    }

    public DataObjectRepository(CosmosContainerProvider containerProvider)
    {
        _dataObjects = containerProvider.GetContainer("DataObjects");
        _dataSets = containerProvider.GetContainer("DataSets");
    }

    public async Task<IEnumerable<DataObject>> GetByDataSetIdAsync(string dataSetId)
    {
        return await CosmosQueryHelpers.QueryAsync<DataObject>(
            _dataObjects,
            new QueryDefinition("SELECT * FROM c WHERE c.DataSetId = @dataSetId")
                .WithParameter("@dataSetId", dataSetId));
    }

    public async Task<DataObject> GetByIdAsync(string id)
    {
        try
        {
            var response = await _dataObjects.ReadItemAsync<DataObject>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"DataObject with ID {id} not found");
        }
    }

    public async Task<DataObject> GetByIdWithIndexAsync(string id)
    {
        var dataObject = await GetByIdAsync(id);

        // Backwards compatibility: populate OriginalDataFile and OriginalIndex if needed
        bool needsUpdate = false;

        // If OriginalDataFile is blank, populate it with the first data file name from parent dataset
        if (string.IsNullOrEmpty(dataObject.OriginalDataFile))
        {
            DataSet? dataset = null;
            try
            {
                var datasetResponse = await _dataSets.ReadItemAsync<DataSet>(dataObject.DataSetId, new PartitionKey(dataObject.DataSetId));
                dataset = datasetResponse.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                dataset = null;
            }

            if (dataset?.DataFiles != null && dataset.DataFiles.Any())
            {
                dataObject.OriginalDataFile = dataset.DataFiles[0].FileName;
                needsUpdate = true;
            }
        }

        // If OriginalIndex is -1, populate it with the index in the filtered query results
        if (dataObject.OriginalIndex == -1)
        {
            var allDataObjects = (await GetByDataSetIdAsync(dataObject.DataSetId)).ToList();
            
            var index = allDataObjects.FindIndex(x => x.Id == dataObject.Id);
            if (index >= 0)
            {
                dataObject.OriginalIndex = index;
                needsUpdate = true;
            }
        }

        // Update the data object if we populated any missing fields
        if (needsUpdate)
        {
            dataObject.UpdatedAt = DateTime.UtcNow;
            await _dataObjects.ReplaceItemAsync(dataObject, dataObject.Id, new PartitionKey(dataObject.Id));
        }

        return dataObject;
    }

    public async Task<IEnumerable<DataObject>> CreateManyAsync(IEnumerable<DataObject> dataObjects)
    {
        var dataObjectsList = dataObjects.ToList();
        var groupedObjects = dataObjectsList.GroupBy(x => x.DataSetId);
        
        foreach (var group in groupedObjects)
        {
            var dataSetId = group.Key;
            var objects = group.ToList();
            var count = objects.Count;
            
            // Update the count in the dataset
            await _dataSets.PatchItemAsync<DataSet>(
                id: dataSetId,
                partitionKey: new PartitionKey(dataSetId),
                patchOperations: new[] { PatchOperation.Increment("/DataObjectCount", count) });
            
            // Set IDs for new objects
            foreach (var obj in objects)
            {
                if (string.IsNullOrEmpty(obj.Id))
                {
                    obj.Id = Guid.NewGuid().ToString();
                }
            }

            foreach (var obj in objects)
            {
                await _dataObjects.CreateItemAsync(obj, new PartitionKey(obj.Id));
            }
        }
        
        return dataObjectsList;
    }

    public async Task DeleteByDataSetIdAsync(string dataSetId)
    {
        // Get total count of objects to be deleted for dataset count update
        var totalCount = await CosmosQueryHelpers.QueryScalarAsync<int>(
            _dataObjects,
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.DataSetId = @dataSetId")
                .WithParameter("@dataSetId", dataSetId));

        if (totalCount > 0)
        {
            await _dataSets.PatchItemAsync<DataSet>(
                id: dataSetId,
                partitionKey: new PartitionKey(dataSetId),
                patchOperations: new[] { PatchOperation.Increment("/DataObjectCount", -totalCount) });
        }

        var idsToDelete = await CosmosQueryHelpers.QueryAsync<string>(
            _dataObjects,
            new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.DataSetId = @dataSetId")
                .WithParameter("@dataSetId", dataSetId));

        foreach (var id in idsToDelete)
        {
            await _dataObjects.DeleteItemAsync<DataObject>(id, new PartitionKey(id));
        }
    }

    public async Task UpdateManyAsync(IEnumerable<DataObject> dataObjects)
    {
        var dataObjectsList = dataObjects.ToList();
        var now = DateTime.UtcNow;

        foreach (var dataObject in dataObjectsList)
        {
            dataObject.UpdatedAt = now;
            await _dataObjects.ReplaceItemAsync(dataObject, dataObject.Id, new PartitionKey(dataObject.Id));
        }
    }
} 