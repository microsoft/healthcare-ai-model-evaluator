using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Repositories;

public class DataSetRepository : IDataSetRepository
{
    private readonly IMongoCollection<DataSet> _collection;
    private readonly IDataObjectRepository _dataObjectRepository;

    public DataSetRepository(IMongoDatabase database, IDataObjectRepository dataObjectRepository)
    {
        _collection = database.GetCollection<DataSet>("DataSets");
        _dataObjectRepository = dataObjectRepository;
    }

    public async Task<IEnumerable<DataSet>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<DataSet> GetByIdAsync(string id)
    {
        var dataset = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (dataset == null)
            throw new KeyNotFoundException($"DataSet with ID {id} not found");
        return dataset;
    }

    public async Task<DataSet> CreateAsync(DataSet dataset)
    {
        if (string.IsNullOrEmpty(dataset.Id))
        {
            dataset.Id = ObjectId.GenerateNewId().ToString();
        }
        await _collection.InsertOneAsync(dataset);
        return dataset;
    }

    public async Task<DataSet> UpdateAsync(DataSet dataset)
    {
        var result = await _collection.ReplaceOneAsync(x => x.Id == dataset.Id, dataset);
        if (result.ModifiedCount == 0)
            throw new KeyNotFoundException($"DataSet with ID {dataset.Id} not found");
        return dataset;
    }

    public async Task DeleteAsync(string id)
    {
        await _dataObjectRepository.DeleteByDataSetIdAsync(id);
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"DataSet with ID {id} not found");
    }
} 