using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MedBench.Core.Models;

public class Trial
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string ExperimentId { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string ExperimentStatus { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public List<DataContent> ModelInputs { get; set; } = new();
    public string? DataObjectId { get; set; }
    public List<TrialFlag> Flags { get; set; } = new();
    public string? DataSetId { get; set; }
    public List<ModelOutput> ModelOutputs { get; set; } = new();
    public TrialResponse Response { get; set; } = new();
    public string? TrialResponse { get; set; } = string.Empty;
    public string? TrialErrorText { get; set; } = string.Empty;
    public string? ReviewerInstructions { get; set; } = string.Empty;
    public double TotalTime { get; set; } = 0;  // Total accumulated time in minutes
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime StartedOn { get; set; }
    public List<BoundingBox> BoundingBoxes { get; set; } = new();

    public string? TestScenarioId { get; set; } = string.Empty;
    public List<EvalQuestion> Questions { get; set; } = new List<EvalQuestion>();

    public bool AllowOutputEditing { get; set; } = false;
}

public class TrialFlag
{
    public string ModelId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> FlagTags { get; set; } = new();
}

public class TrialResponse
{
    public string ModelId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class ModelOutput
{
    public string ModelId { get; set; } = string.Empty;
    public List<DataContent> Output { get; set; } = new();
}

public class BoundingBox
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public string? ModelId { get; set; } = string.Empty;
    public double Height { get; set; }
    public string? Annotation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CoordinateType { get; set; } = "pixel";
}
