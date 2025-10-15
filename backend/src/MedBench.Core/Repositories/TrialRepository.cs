using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MedBench.Core.Repositories;

public class TrialRepository : ITrialRepository
{
    private readonly IMongoCollection<Trial> _collection;
    private readonly ILogger<TrialRepository> _logger;

    public TrialRepository(IMongoDatabase database, ILogger<TrialRepository> logger)
    {
        _collection = database.GetCollection<Trial>("Trials");

        // Create indexes
        var indexKeysDefinition = Builders<Trial>.IndexKeys.Ascending(x => x.ExperimentId);
        _collection.Indexes.CreateOne(new CreateIndexModel<Trial>(indexKeysDefinition));

        // Add compound index for pending trials query (supports filtering and sorting)
        var pendingTrialsIndex = Builders<Trial>.IndexKeys
            .Ascending(x => x.UserId)
            .Ascending(x => x.ExperimentStatus)
            .Ascending(x => x.Status);
        _collection.Indexes.CreateOne(new CreateIndexModel<Trial>(pendingTrialsIndex));

        // Add simple UserId index for other user-based queries
        var userIdIndex = Builders<Trial>.IndexKeys.Ascending(x => x.UserId);
        _collection.Indexes.CreateOne(new CreateIndexModel<Trial>(userIdIndex));

        // Add compound index for experiment and data object queries
        var experimentDataObjectIndex = Builders<Trial>.IndexKeys
            .Ascending(x => x.ExperimentId)
            .Ascending(x => x.DataObjectId);
        _collection.Indexes.CreateOne(new CreateIndexModel<Trial>(experimentDataObjectIndex));

        _logger = logger;
    }

    public async Task<IEnumerable<Trial>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<Trial> GetByIdAsync(string id)
    {
        var trial = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (trial == null)
            throw new KeyNotFoundException($"Trial with ID {id} not found");
        
        _logger.LogInformation($"Retrieved trial from DB: {trial.ToJson()}");
        return trial;
    }

    public async Task<IEnumerable<Trial>> GetByUserIdAsync(string userId)
    {
        return await _collection.Find(x => x.UserId == userId).ToListAsync();
    }

    public async Task<IEnumerable<Trial>> GetByExperimentIdAsync(string experimentId)
    {
        return await _collection.Find(t => t.ExperimentId == experimentId)
                          .ToListAsync();
    }

    public async Task<Trial> CreateAsync(Trial trial)
    {
        trial.StartedOn = DateTime.UtcNow;
        trial.CreatedAt = DateTime.UtcNow;
        trial.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(trial);
        return trial;
    }

    public async Task<Trial> UpdateAsync(Trial trial)
    {

        try
        {
            var result = await _collection.ReplaceOneAsync(
                x => x.Id == trial.Id,
                trial
            );

            if (result.ModifiedCount == 0)
                throw new KeyNotFoundException($"Trial with ID {trial.Id} not found");

            return trial;
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    public async Task DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"Trial with ID {id} not found");
    }

    public async Task DeleteByExperimentIdAsync(string experimentId)
    {
        const int batchSize = 50;
        List<string> idsToDelete;
        
        do
        {
            // Find documents to delete in batches
            idsToDelete = await _collection
                .Find(x => x.ExperimentId == experimentId)
                .Limit(batchSize)
                .Project(x => x.Id)
                .ToListAsync();
            
            if (idsToDelete.Count > 0)
            {
                // Delete the batch by IDs
                var result = await _collection.DeleteManyAsync(x => idsToDelete.Contains(x.Id));
                _logger.LogInformation($"Deleted {result.DeletedCount} trials for experiment {experimentId}");
            }
            
        } while (idsToDelete.Count > 0);
    }

    public async Task UpdateExperimentStatusAsync(string experimentId, string status)
    {
        const int batchSize = 50;
        List<string> idsToUpdate;
        
        do
        {
            // Find documents to update in batches (only those that don't already have the target status)
            idsToUpdate = await _collection
                .Find(x => x.ExperimentId == experimentId && x.ExperimentStatus != status)
                .Limit(batchSize)
                .Project(x => x.Id)
                .ToListAsync();
            
            if (idsToUpdate.Count > 0)
            {
                // Update the batch by IDs
                var update = Builders<Trial>.Update.Set(x => x.ExperimentStatus, status);
                var result = await _collection.UpdateManyAsync(x => idsToUpdate.Contains(x.Id), update);
                _logger.LogInformation($"Updated {result.ModifiedCount} trials for experiment {experimentId} to status {status}");
            }
            
        } while (idsToUpdate.Count > 0);
    }

    public async Task<IEnumerable<Trial>> GetPendingTrialsAsync(string userId)
    {
       var pendingFilter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(x => x.UserId, userId),
            Builders<Trial>.Filter.Eq(x => x.Status, "pending"),
            Builders<Trial>.Filter.Eq(x => x.ExperimentStatus, "InProgress")
        );
        var trials = await _collection
            .Find(pendingFilter)
            .ToListAsync();
        if (trials.Count > 0)
        {
            return trials;
        }

       
        var skippedFilter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(x => x.UserId, userId),
            Builders<Trial>.Filter.Eq(x => x.Status, "skipped"),
            Builders<Trial>.Filter.Eq(x => x.ExperimentStatus, "InProgress")
        );
        var skippedTrials = await _collection
            .Find(skippedFilter)
            .ToListAsync();
        if (skippedTrials.Count > 0)
        {
            return skippedTrials;
        }
        // If no matching trials, return empty
        return new List<Trial>();
    }
    public async Task<IEnumerable<string>> GetDoneTrialIdsAsync(string userId, string[] experimentIds)
    {
        var filter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(t => t.UserId, userId),
            Builders<Trial>.Filter.In(t => t.ExperimentId, experimentIds),
            Builders<Trial>.Filter.Eq(t => t.Status, "done"),
            Builders<Trial>.Filter.Eq(t => t.ExperimentStatus, "InProgress")
        );

        var trialids = await _collection.Find(filter)
            .Project(t => t.Id)
            .ToListAsync();
        return trialids;         
    }
    
    public async Task<IEnumerable<string>> UnskipTrialsAsync(string userId, string testScenarioId)
    {
        var filter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(t => t.UserId, userId),
            Builders<Trial>.Filter.Eq(t => t.TestScenarioId, testScenarioId),
            Builders<Trial>.Filter.Eq(t => t.Status, "skipped"),
            Builders<Trial>.Filter.Eq(t => t.ExperimentStatus, "InProgress")
        );

        var trialids = await _collection.Find(filter)
            .Project(t => t.Id)
            .ToListAsync();

        if (trialids.Count > 0)
        {
            var update = Builders<Trial>.Update.Set(t => t.Status, "pending");
            var result = await _collection.UpdateManyAsync(filter, update);
            _logger.LogInformation($"Unskipped {result.ModifiedCount} trials for user {userId} in test scenario {testScenarioId}");
        }

        return trialids;         
    }

    public async Task<int> GetPendingTrialCountForTestScenarioAsync(string userId, string testScenarioId)
    {
        var filter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(t => t.UserId, userId),
            Builders<Trial>.Filter.Eq(t => t.TestScenarioId, testScenarioId),
            Builders<Trial>.Filter.Eq(t => t.Status, "pending"),
            Builders<Trial>.Filter.Eq(t => t.ExperimentStatus, "InProgress")
        );  

        return (int)await _collection.CountDocumentsAsync(filter);
    }
    public async Task<IEnumerable<string>> GetPendingTrialIdsAsync(string userId, string[]? experimentIds, string? testScenarioId)
    {
        if (experimentIds == null && testScenarioId == null)
        {
            throw new ArgumentException("Either experimentIds or testScenarioId must be provided");
        }
        if (experimentIds == null)
        {
            experimentIds = Array.Empty<string>();
        }
        if (testScenarioId != null)
        {
            var filterByTestScenario = Builders<Trial>.Filter.And(
                Builders<Trial>.Filter.Eq(t => t.UserId, userId),
                Builders<Trial>.Filter.Eq(t => t.TestScenarioId, testScenarioId),
                Builders<Trial>.Filter.Eq(t => t.Status, "pending"),
                Builders<Trial>.Filter.Eq(t => t.ExperimentStatus, "InProgress")
            );
            var trialidsByTestScenario = await _collection.Find(filterByTestScenario)
                .Project(t => t.Id)
                .ToListAsync();
            if (trialidsByTestScenario.Count > 0)
            {
                return trialidsByTestScenario;
            }
            var skippedFilterByTestScenario = Builders<Trial>.Filter.And(
                Builders<Trial>.Filter.Eq(t => t.UserId, userId),
                Builders<Trial>.Filter.Eq(t => t.TestScenarioId, testScenarioId),
                Builders<Trial>.Filter.Eq(t => t.Status, "skipped"),
                Builders<Trial>.Filter.Eq(t => t.ExperimentStatus, "InProgress")
            );
            var skippedTrialIdsByTestScenario = await _collection.Find(skippedFilterByTestScenario)
                .Project(t => t.Id)
                .ToListAsync();
            return skippedTrialIdsByTestScenario;
        }
        var filter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(t => t.UserId, userId),
            Builders<Trial>.Filter.In(t => t.ExperimentId, experimentIds),
            Builders<Trial>.Filter.Eq(t => t.Status, "pending"),
            Builders<Trial>.Filter.Eq(t => t.ExperimentStatus, "InProgress")
        );

        var trialids = await _collection.Find(filter)
            .Project(t => t.Id)
            .ToListAsync();
        if (trialids.Count > 0)
        {
            return trialids;
        }
        var skippedFilter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(t => t.UserId, userId),
            Builders<Trial>.Filter.In(t => t.ExperimentId, experimentIds),
            Builders<Trial>.Filter.Eq(t => t.Status, "skipped"),
            Builders<Trial>.Filter.Eq(t => t.ExperimentStatus, "InProgress")
        );
        var skippedTrialIds = await _collection.Find(skippedFilter)
            .Project(t => t.Id)
            .ToListAsync();
        return skippedTrialIds;
    }

    public async Task<Dictionary<string, int>> GetPendingTrialCountsByType(string userId, string[] validStatuses, string[] validExperimentStatuses)
    {
        Console.WriteLine("GetPendingTrialCountsByType called");
        Console.WriteLine("userId: " + userId);
        Console.WriteLine("validStatuses: " + string.Join(", ", validStatuses));
        Console.WriteLine("validExperimentStatuses: " + string.Join(", ", validExperimentStatuses));
        var filter = Builders<Trial>.Filter.And(
            Builders<Trial>.Filter.Eq(t => t.UserId, userId),
            Builders<Trial>.Filter.In(t => t.Status, validStatuses),
            Builders<Trial>.Filter.In(t => t.ExperimentStatus, validExperimentStatuses)
        );

        var trials = await _collection.Find(filter).ToListAsync();

        var counts = new Dictionary<string, int>
        {
            { "Simple Evaluation", 0 },
            { "Simple Validation", 0 },
            { "Arena", 0 },
            { "Full Validation", 0 }
        };

        foreach (var trial in trials)
        {
            if (counts.ContainsKey(trial.ExperimentType))
            {
                counts[trial.ExperimentType]++;
            }
        }

        return counts;
    }

    public async Task<Trial> GetNextPendingTrialAsync(string userId)
    {
        var trial = await _collection.Find(x => 
            x.UserId == userId && 
            (x.Status == "pending" || x.Status == "skipped") && 
            x.ExperimentStatus == "InProgress")
            .FirstOrDefaultAsync();

        if (trial != null)
        {
            trial.StartedOn = DateTime.UtcNow;
            await UpdateAsync(trial);
        }
        else
        {
            Console.WriteLine("No pending trial found");
            throw new Exception("No pending trial found");
        }

        return trial;
    }

    public async Task<IEnumerable<Trial>> GetTrialsByExperimentAndDataObject(string experimentId, string dataObjectId)
    {
        return await _collection.Find(x => 
            x.ExperimentId == experimentId && 
            x.DataObjectId == dataObjectId)
            .ToListAsync();
    }

    public async Task<int> GetPendingTrialCountForExperiment(string experimentId)
    {
        return (int)await _collection.CountDocumentsAsync(
            x => x.ExperimentId == experimentId && x.Status == "pending"
        );
    }
} 