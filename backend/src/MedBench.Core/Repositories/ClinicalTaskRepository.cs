using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Repositories;

public class ClinicalTaskRepository : IClinicalTaskRepository
{
    private readonly IMongoCollection<ClinicalTask> _collection;

    public ClinicalTaskRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ClinicalTask>("ClinicalTasks");
    }

    public async Task<IEnumerable<ClinicalTask>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<ClinicalTask> GetByIdAsync(string id)
    {
        var task = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (task == null)
            throw new KeyNotFoundException($"ClinicalTask with ID {id} not found");
        return task;
    }

    public async Task<IEnumerable<ClinicalTask>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var filter = Builders<ClinicalTask>.Filter.In(x => x.Id, ids);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<ClinicalTask> CreateAsync(ClinicalTask clinicalTask)
    {
        clinicalTask.Id = ObjectId.GenerateNewId().ToString();
        clinicalTask.CreatedAt = DateTime.UtcNow;
        clinicalTask.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(clinicalTask);
        return clinicalTask;
    }

    public async Task<ClinicalTask> UpdateAsync(ClinicalTask clinicalTask)
    {
        clinicalTask.UpdatedAt = DateTime.UtcNow;
        var result = await _collection.ReplaceOneAsync(x => x.Id == clinicalTask.Id, clinicalTask);
        if (result.ModifiedCount == 0)
            throw new KeyNotFoundException($"ClinicalTask with ID {clinicalTask.Id} not found");
        return clinicalTask;
    }

    public async Task DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"ClinicalTask with ID {id} not found");
    }
} 