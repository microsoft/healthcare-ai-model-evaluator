using System.Text.Json.Serialization;

namespace MedBench.Core.Models;

public class Image
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    public string StorageAccount { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
} 