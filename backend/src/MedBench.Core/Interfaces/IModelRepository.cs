namespace MedBench.Core.Interfaces;

public interface IModelRepository
{
    Task<IEnumerable<Model>> GetAllAsync();
    Task<Model> GetByIdAsync(string id);
    Task<Model> CreateAsync(Model model);
    Task<Model> UpdateAsync(Model model);
    Task DeleteAsync(string id);
    
    /// <summary>
    /// Gets a model with all integration settings loaded (including secrets from Key Vault)
    /// This should be used when the model will be used for actual operations
    /// </summary>
    /// <param name="id">Model ID</param>
    /// <returns>Model with complete integration settings</returns>
    Task<Model> GetByIdWithSecretsAsync(string id);
    
    /// <summary>
    /// Gets a model for display purposes with sensitive values masked
    /// This should be used when returning models to the frontend
    /// </summary>
    /// <param name="id">Model ID</param>
    /// <returns>Model with display-safe integration settings</returns>
    Task<Model> GetByIdForDisplayAsync(string id);
} 