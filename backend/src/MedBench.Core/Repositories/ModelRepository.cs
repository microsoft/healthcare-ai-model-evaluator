using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;

namespace MedBench.Core.Repositories;

public class ModelRepository : IModelRepository
{
    private readonly IMongoCollection<Model> _collection;

    public ModelRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Model>("Models");
    }

    public async Task<IEnumerable<Model>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<Model> GetByIdAsync(string id)
    {
        var model = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (model == null)
            throw new KeyNotFoundException($"Model with ID {id} not found");
        return model;
    }

    public async Task<Model> CreateAsync(Model model)
    {
        model.Id = ObjectId.GenerateNewId().ToString();
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(model);
        return model;
    }

    public async Task<Model> UpdateAsync(Model model)
    {
        model.UpdatedAt = DateTime.UtcNow;
        var result = await _collection.ReplaceOneAsync(x => x.Id == model.Id, model);
        if (result.ModifiedCount == 0)
            throw new KeyNotFoundException($"Model with ID {model.Id} not found");
        return model;
    }

    public async Task DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"Model with ID {id} not found");
    }
} 