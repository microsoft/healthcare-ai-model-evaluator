using System.Text.Json.Serialization;

namespace MedBench.Core.Models;

public class Experiment
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExperimentStatus Status { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProcessingStatus ProcessingStatus { get; set; }

    public string TestScenarioId { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> ReviewerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string OwnerId { get; set; } = string.Empty;
    public int? PendingTrials { get; set; }
    public int? TotalTrials { get; set; }
    public List<string> AssignedUserIds { get; set; } = new();
    public double TotalCost { get; set; } = 0;
    public string? ReviewerInstructions { get; set; }

    public Boolean Randomized { get; set; } = true;
}

public enum ProcessingStatus
{
    NotProcessed,
    Processing,
    Processed,
    Finalizing,
    Final,
    Error
} 