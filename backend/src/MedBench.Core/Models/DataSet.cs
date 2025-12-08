using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MedBench.Core.Models;

public enum DataFileProcessingStatus
{
    Unprocessed,
    Processing,
    Completed,
    Failed
}

public class DataFileMappingItem
{
    public string Type { get; set; } = "text"; // "text" or "imageurl"
    public List<string> KeyPath { get; set; } = new List<string>();
    public bool IsArray { get; set; } = false; // New property to indicate this mapping processes an array
}

public class DataFileMapping
{
    public List<DataFileMappingItem> InputMappings { get; set; } = new List<DataFileMappingItem>();
    public List<DataFileMappingItem> OutputMappings { get; set; } = new List<DataFileMappingItem>();
}

public class DataFile
{
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public DataFileProcessingStatus ProcessingStatus { get; set; } = DataFileProcessingStatus.Unprocessed;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DataFileMapping Mapping { get; set; } = new DataFileMapping();
    public int ProcessedObjectCount { get; set; } = 0;
    public int TotalObjectCount { get; set; } = 0;
}

public class DataSet
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReviewerInstructions { get; set; } = string.Empty;
    public string AiModelType { get; set; } = string.Empty;  // Will store "text-to-text" or "image-to-text"
    public List<string> Tags { get; set; } = new List<string>();
    public int DataObjectCount { get; set; }
    public int ModelOutputCount { get; set; }
    public int ModelInputCount { get; set; }
    public List<string> GeneratedDataList { get; set; } = new List<string>(); //List of all the times data was generated for this dataset (model name (datetime))
    public int TotalTokens { get; set; } = 0;
    public int TotalInputTokens { get; set; } = 0;
    public int TotalOutputTokens { get; set; } = 0;
    public Dictionary<string, int> TotalOutputTokensPerIndex { get; set; } = new Dictionary<string, int>();
    public List<DataFile> DataFiles { get; set; } = new List<DataFile>();
    
    // Data retention settings
    public int DaysToAutoDelete { get; set; } = 180; // Default 180 days
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
} 

