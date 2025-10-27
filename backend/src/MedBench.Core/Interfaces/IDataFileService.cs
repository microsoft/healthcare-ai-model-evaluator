using MedBench.Core.Models;
using Microsoft.AspNetCore.Http;

namespace MedBench.Core.Interfaces;

public interface IDataFileService
{
    Task<string> UploadDataFileAsync(IFormFile file, string datasetId);
    Task<Stream> GetDataFileStreamAsync(string blobUrl);
    Task ProcessDataFileAsync(DataSet dataset, DataFile dataFile);
    Task DeleteDataFileAsync(string blobUrl);
    int EstimateImageTokens(string dataUrl);
} 