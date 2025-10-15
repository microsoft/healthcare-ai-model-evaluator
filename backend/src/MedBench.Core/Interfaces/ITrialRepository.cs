namespace MedBench.Core.Interfaces;

public interface ITrialRepository
{
    Task<IEnumerable<Trial>> GetAllAsync();
    Task<Trial> GetByIdAsync(string id);
    Task<IEnumerable<Trial>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Trial>> GetByExperimentIdAsync(string experimentId);
    Task<Trial> CreateAsync(Trial trial);
    Task<Trial> UpdateAsync(Trial trial);
    Task DeleteAsync(string id);
    Task DeleteByExperimentIdAsync(string experimentId);
    Task UpdateExperimentStatusAsync(string experimentId, string status);
    Task<IEnumerable<Trial>> GetPendingTrialsAsync(string userId);
    Task<int> GetPendingTrialCountForTestScenarioAsync(string userId, string testScenarioId);
    Task<IEnumerable<string>> GetPendingTrialIdsAsync(string userId, string[]? experimentIds = null, string? testScenarioId = null);
    Task<IEnumerable<string>> UnskipTrialsAsync(string userId, string testScenarioId);
    Task<IEnumerable<string>> GetDoneTrialIdsAsync(string userId, string[] experimentIds);
    Task<Dictionary<string, int>> GetPendingTrialCountsByType(string userId, string[] validStatuses, string[] validExperimentStatuses);
    Task<IEnumerable<Trial>> GetTrialsByExperimentAndDataObject(string experimentId, string dataObjectId);
    Task<int> GetPendingTrialCountForExperiment(string experimentId);
} 