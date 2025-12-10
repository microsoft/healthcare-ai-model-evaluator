using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MedBench.Core.Models;

public class DataContent {
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string GeneratedForClinicalTask { get; set; } = string.Empty;
    public int TotalTokens { get; set; } = 0;
    public string ContentUrl { get; set; } = string.Empty;
}
public class DataObject
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    [BsonRepresentation(BsonType.ObjectId)]
    public string DataSetId { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DataContent> InputData { get; set; } = new();
    public List<DataContent> OutputData { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DataContent> GeneratedOutputData { get; set; } = new();
    public int TotalTokens { get; set; } = 0;
    public int TotalInputTokens { get; set; } = 0;
    public int TotalOutputTokens { get; set; } = 0;
    public Dictionary<string, int> TotalOutputTokensPerIndex { get; set; } = new Dictionary<string, int>();

    public int OriginalIndex { get; set; } = -1;
    public string OriginalDataFile { get; set; } = string.Empty;
} 