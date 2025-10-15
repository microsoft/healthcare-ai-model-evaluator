using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace MedBench.Core.Models;

public class TestScenario
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReviewerInstructions { get; set; } = string.Empty;
    public List<string> ModelIds { get; set; } = new List<string>();
    public List<string> Tags { get; set; } = new List<string>();
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("EvalMetric")]
    [BsonIgnoreIfNull]
    public string? EvalMetric { get; set; }

    public List<EvalQuestion> Questions { get; set; } = new List<EvalQuestion>();
    public Boolean AllowOutputEditing { get; set; } = false;
    public string? ExperimentType { get; set; } = "Single Evaluation"; // Default to single evaluation
} 