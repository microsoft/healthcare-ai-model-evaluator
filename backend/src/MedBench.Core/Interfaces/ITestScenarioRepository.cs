using MedBench.Core.Models;

namespace MedBench.Core.Interfaces;

public interface ITestScenarioRepository
{
    Task<IEnumerable<TestScenario>> GetAllAsync();
    Task<TestScenario> GetByIdAsync(string id);
    Task<IEnumerable<TestScenario>> GetByIdsAsync(IEnumerable<string> ids);
    Task<TestScenario> CreateAsync(TestScenario testScenario);
    Task<TestScenario> UpdateAsync(TestScenario testScenario);
    Task DeleteAsync(string id);
    Task<IEnumerable<TestScenario>> GetByClinicalTaskIdsAsync(List<string> taskIds);
} 