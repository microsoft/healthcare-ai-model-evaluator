namespace MedBench.Core.Interfaces;

public interface IClinicalTaskRepository
{
    Task<IEnumerable<ClinicalTask>> GetAllAsync();
    Task<ClinicalTask> GetByIdAsync(string id);
    Task<IEnumerable<ClinicalTask>> GetByIdsAsync(IEnumerable<string> ids);
    Task<ClinicalTask> CreateAsync(ClinicalTask clinicalTask);
    Task<ClinicalTask> UpdateAsync(ClinicalTask clinicalTask);
    Task DeleteAsync(string id);
} 