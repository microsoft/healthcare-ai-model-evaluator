using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MedBench.Core.Repositories;

public class ExperimentRepository : IExperimentRepository
{
    private readonly IMongoCollection<Experiment> _experiments;

    public ExperimentRepository(IMongoDatabase database)
    {
        _experiments = database.GetCollection<Experiment>("Experiments");

        // Create indexes for common queries
        
        // Index for processing status queries
        var processingStatusIndex = Builders<Experiment>.IndexKeys.Ascending(e => e.ProcessingStatus);
        _experiments.Indexes.CreateOne(new CreateIndexModel<Experiment>(processingStatusIndex));

        // Index for test scenario lookup
        var testScenarioIndex = Builders<Experiment>.IndexKeys.Ascending(e => e.TestScenarioId);
        _experiments.Indexes.CreateOne(new CreateIndexModel<Experiment>(testScenarioIndex));

        // Index for owner lookup
        var ownerIndex = Builders<Experiment>.IndexKeys.Ascending(e => e.OwnerId);
        _experiments.Indexes.CreateOne(new CreateIndexModel<Experiment>(ownerIndex));

        // Index for reviewer IDs (array field)
        var reviewerIndex = Builders<Experiment>.IndexKeys.Ascending(e => e.ReviewerIds);
        _experiments.Indexes.CreateOne(new CreateIndexModel<Experiment>(reviewerIndex));

        // Index for assigned user IDs (array field)
        var assignedUserIndex = Builders<Experiment>.IndexKeys.Ascending(e => e.AssignedUserIds);
        _experiments.Indexes.CreateOne(new CreateIndexModel<Experiment>(assignedUserIndex));
    }

    public async Task<IEnumerable<Experiment>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var filter = Builders<Experiment>.Filter.In(e => e.Id, ids);
        return await _experiments.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<Experiment>> GetAllAsync()
    {
        return await _experiments.Find(_ => true).ToListAsync();
    }

    public async Task<Experiment> GetByIdAsync(string id)
    {
        var experiment = await _experiments.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (experiment == null)
            throw new KeyNotFoundException($"Experiment with ID {id} not found");
        return experiment;
    }

    public async Task<Experiment> CreateAsync(Experiment experiment)
    {
        // Ensure the object has an ID
        if (string.IsNullOrEmpty(experiment.Id))
        {
            experiment.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        }
        
        // Make sure timestamps are set
        if (experiment.CreatedAt == default)
        {
            experiment.CreatedAt = DateTime.UtcNow;
        }
        
        if (experiment.UpdatedAt == default)
        {
            experiment.UpdatedAt = DateTime.UtcNow;
        }
        
        await _experiments.InsertOneAsync(experiment);
        return experiment;
    }

    public async Task<Experiment> UpdateAsync(Experiment experiment)
    {
        var result = await _experiments.ReplaceOneAsync(e => e.Id == experiment.Id, experiment);
        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"Experiment with ID {experiment.Id} not found");
        return experiment;
    }

    public async Task DeleteAsync(string id)
    {
        var result = await _experiments.DeleteOneAsync(e => e.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"Experiment with ID {id} not found");
    }

    public async Task<IEnumerable<Experiment>> GetByUserIdAsync(string userId)
    {
        var filter = Builders<Experiment>.Filter.Eq(e => e.OwnerId, userId) |
                     Builders<Experiment>.Filter.AnyEq(e => e.ReviewerIds, userId) |
                     Builders<Experiment>.Filter.AnyEq(e => e.AssignedUserIds, userId);
        return await _experiments.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<Experiment>> GetByProcessingStatusAsync(ProcessingStatus status)
    {
        return await _experiments.Find(e => e.ProcessingStatus == status).ToListAsync();
    }

    public async Task<IEnumerable<Experiment>> GetByTestScenarioIdsAsync(List<string> scenarioIds)
    {
        // Find all experiments where the TestScenarioId is in the provided list
        var filter = Builders<Experiment>.Filter.In(e => e.TestScenarioId, scenarioIds);
        return await _experiments.Find(filter).ToListAsync();
    }
} 