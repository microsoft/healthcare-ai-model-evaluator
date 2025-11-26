using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Repositories;

public class DataObjectRepository : IDataObjectRepository
{
    private readonly IMongoCollection<DataObject> _collection;
    private readonly IMongoCollection<DataSet> _dataSetCollection;

    public DataObjectRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<DataObject>("DataObjects");
        _dataSetCollection = database.GetCollection<DataSet>("DataSets");
        
        // Create sharded index on DataSetId
        var indexKeysDefinition = Builders<DataObject>.IndexKeys.Ascending(d => d.DataSetId);
        var indexOptions = new CreateIndexOptions { Name = "DataSetId_1" };
        var indexModel = new CreateIndexModel<DataObject>(indexKeysDefinition, indexOptions);
        _collection.Indexes.CreateOne(indexModel);
    }

    public async Task<IEnumerable<DataObject>> GetByDataSetIdAsync(string dataSetId)
    {
        return await _collection.Find(x => x.DataSetId == dataSetId).ToListAsync();
    }

    public async Task<DataObject> GetByIdAsync(string id)
    {
        var dataObject = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (dataObject == null)
            throw new KeyNotFoundException($"DataObject with ID {id} not found");
        return dataObject;
    }

    public async Task<DataObject> GetByIdWithIndexAsync(string id)
    {
        var dataObject = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (dataObject == null)
            throw new KeyNotFoundException($"DataObject with ID {id} not found");

        // Backwards compatibility: populate OriginalDataFile and OriginalIndex if needed
        bool needsUpdate = false;

        // If OriginalDataFile is blank, populate it with the first data file name from parent dataset
        if (string.IsNullOrEmpty(dataObject.OriginalDataFile))
        {
            var dataset = await _dataSetCollection.Find(x => x.Id == dataObject.DataSetId).FirstOrDefaultAsync();
            if (dataset?.DataFiles != null && dataset.DataFiles.Any())
            {
                dataObject.OriginalDataFile = dataset.DataFiles[0].FileName;
                needsUpdate = true;
            }
        }

        // If OriginalIndex is -1, populate it with the index in the filtered query results
        if (dataObject.OriginalIndex == -1)
        {
            var allDataObjects = await _collection
                .Find(x => x.DataSetId == dataObject.DataSetId)
                .ToListAsync();
            
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
            var filter = Builders<DataObject>.Filter.Eq(x => x.Id, dataObject.Id);
            dataObject.UpdatedAt = DateTime.UtcNow;
            await _collection.ReplaceOneAsync(filter, dataObject);
        }

        return dataObject;
    }

    public async Task<IEnumerable<DataObject>> CreateManyAsync(IEnumerable<DataObject> dataObjects)
    {
        const int batchSize = 100;
        var dataObjectsList = dataObjects.ToList();
        var groupedObjects = dataObjectsList.GroupBy(x => x.DataSetId);
        
        foreach (var group in groupedObjects)
        {
            var dataSetId = group.Key;
            var objects = group.ToList();
            var count = objects.Count;
            
            // Update the count in the dataset
            var update = Builders<DataSet>.Update.Inc(x => x.DataObjectCount, count);
            await _dataSetCollection.UpdateOneAsync(x => x.Id == dataSetId, update);
            
            // Set IDs for new objects
            foreach (var obj in objects)
            {
                if (string.IsNullOrEmpty(obj.Id))
                {
                    obj.Id = ObjectId.GenerateNewId().ToString();
                }
            }
            
            // Insert in batches to avoid overwhelming the database
            for (int i = 0; i < objects.Count; i += batchSize)
            {
                var batch = objects.Skip(i).Take(batchSize);
                await _collection.InsertManyAsync(batch);
            }
        }
        
        return dataObjectsList;
    }

    public async Task DeleteByDataSetIdAsync(string dataSetId)
    {
        const int batchSize = 100;
        
        // Get total count of objects to be deleted for dataset count update
        var totalCount = await _collection.CountDocumentsAsync(x => x.DataSetId == dataSetId);
        
        // Decrease the count in the dataset
        if (totalCount > 0)
        {
            var update = Builders<DataSet>.Update.Inc(x => x.DataObjectCount, -totalCount);
            await _dataSetCollection.UpdateOneAsync(x => x.Id == dataSetId, update);
        }
        
        // Delete in batches to avoid overwhelming the database
        List<string> idsToDelete;
        do
        {
            idsToDelete = await _collection
                .Find(x => x.DataSetId == dataSetId)
                .Limit(batchSize)
                .Project(x => x.Id)
                .ToListAsync();
            
            if (idsToDelete.Count > 0)
            {
                await _collection.DeleteManyAsync(x => idsToDelete.Contains(x.Id));
            }
            
        } while (idsToDelete.Count > 0);
    }

    public async Task UpdateManyAsync(IEnumerable<DataObject> dataObjects)
    {
        const int batchSize = 100;
        var dataObjectsList = dataObjects.ToList();
        var now = DateTime.UtcNow;
        
        // Process in batches to avoid overwhelming the database
        for (int i = 0; i < dataObjectsList.Count; i += batchSize)
        {
            var batch = dataObjectsList.Skip(i).Take(batchSize);
            var bulkOps = new List<WriteModel<DataObject>>();
            
            foreach (var dataObject in batch)
            {
                var filter = Builders<DataObject>.Filter.Eq(x => x.Id, dataObject.Id);
                dataObject.UpdatedAt = now;
                bulkOps.Add(new ReplaceOneModel<DataObject>(filter, dataObject));
            }

            if (bulkOps.Any())
            {
                await _collection.BulkWriteAsync(bulkOps);
            }
        }
    }
} 