using MongoDB.Driver;
using MongoDB.Bson;
using MedBench.Core.Models;
using MedBench.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MedBench.Core.Repositories;

public class TestScenarioRepository : ITestScenarioRepository
{
    private readonly IMongoCollection<TestScenario> _testScenarios;

    public TestScenarioRepository(IMongoDatabase database)
    {
        _testScenarios = database.GetCollection<TestScenario>("TestScenarios");

        // Create indexes for common queries
        
        // Index for clinical task lookup
        var taskIdIndex = Builders<TestScenario>.IndexKeys.Ascending(ts => ts.TaskId);
        _testScenarios.Indexes.CreateOne(new CreateIndexModel<TestScenario>(taskIdIndex));
    }

    public async Task<IEnumerable<TestScenario>> GetAllAsync()
    {
        return await _testScenarios.Find(_ => true).ToListAsync();
    }

    public async Task<TestScenario> GetByIdAsync(string id)
    {
        var testScenario = await _testScenarios.Find(t => t.Id == id).FirstOrDefaultAsync();
        if (testScenario == null)
            throw new KeyNotFoundException($"TestScenario with ID {id} not found");
        return testScenario;
    }
    public async Task<IEnumerable<TestScenario>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var filter = Builders<TestScenario>.Filter.In(ts => ts.Id, ids);
        return await _testScenarios.Find(filter).ToListAsync();
    }

    public async Task<TestScenario> CreateAsync(TestScenario testScenario)
    {
        // Ensure the object has an ID
        if (string.IsNullOrEmpty(testScenario.Id))
        {
            testScenario.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        }

        // Set creation/update timestamps
        testScenario.CreatedAt = DateTime.UtcNow;
        testScenario.UpdatedAt = DateTime.UtcNow;

        await _testScenarios.InsertOneAsync(testScenario);
        return testScenario;
    }

    public async Task<TestScenario> UpdateAsync(TestScenario testScenario)
    {
        var result = await _testScenarios.ReplaceOneAsync(t => t.Id == testScenario.Id, testScenario);
        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"TestScenario with ID {testScenario.Id} not found");
        return testScenario;
    }

    public async Task DeleteAsync(string id)
    {
        var result = await _testScenarios.DeleteOneAsync(t => t.Id == id);
        if (result.DeletedCount == 0)
            throw new KeyNotFoundException($"TestScenario with ID {id} not found");
    }

    public async Task<IEnumerable<TestScenario>> GetByClinicalTaskIdsAsync(List<string> taskIds)
    {
        // Find all test scenarios where the TaskId is in the provided list
        var filter = Builders<TestScenario>.Filter.In(ts => ts.TaskId, taskIds);
        return await _testScenarios.Find(filter).ToListAsync();
    }
} 