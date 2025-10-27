namespace MedBench.Core.Interfaces;

public interface IExperimentRepository
{
    Task<IEnumerable<Experiment>> GetAllAsync();
    Task<Experiment> GetByIdAsync(string id);
    Task<IEnumerable<Experiment>> GetByIdsAsync(IEnumerable<string> ids);
    Task<Experiment> CreateAsync(Experiment experiment);
    Task<Experiment> UpdateAsync(Experiment experiment);
    Task DeleteAsync(string id);
    Task<IEnumerable<Experiment>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Experiment>> GetByProcessingStatusAsync(ProcessingStatus status);
    Task<IEnumerable<Experiment>> GetByTestScenarioIdsAsync(List<string> scenarioIds);
} 