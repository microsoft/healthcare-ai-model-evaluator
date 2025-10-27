namespace MedBench.Core.Interfaces;

public interface IDataObjectRepository
{
    Task<IEnumerable<DataObject>> GetByDataSetIdAsync(string dataSetId);
    Task<DataObject> GetByIdAsync(string id);
    Task<IEnumerable<DataObject>> CreateManyAsync(IEnumerable<DataObject> dataObjects);
    Task DeleteByDataSetIdAsync(string dataSetId);
    Task UpdateManyAsync(IEnumerable<DataObject> dataObjects);
} 