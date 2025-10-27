namespace MedBench.Core.Interfaces;

public interface IModelRepository
{
    Task<IEnumerable<Model>> GetAllAsync();
    Task<Model> GetByIdAsync(string id);
    Task<Model> CreateAsync(Model model);
    Task<Model> UpdateAsync(Model model);
    Task DeleteAsync(string id);
} 