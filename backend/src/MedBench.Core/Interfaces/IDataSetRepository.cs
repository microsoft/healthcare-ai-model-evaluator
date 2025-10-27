namespace MedBench.Core.Interfaces;

public interface IDataSetRepository
{
    Task<IEnumerable<DataSet>> GetAllAsync();
    Task<DataSet> GetByIdAsync(string id);
    Task<DataSet> CreateAsync(DataSet dataset);
    Task<DataSet> UpdateAsync(DataSet dataset);
    Task DeleteAsync(string id);
} 