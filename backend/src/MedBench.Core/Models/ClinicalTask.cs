namespace MedBench.Core.Models;

public class ClinicalTask
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<TaskDataSetModel> DataSetModels { get; set; } = new();
    public string? Prompt { get; set; }
    public List<string> Tags { get; set; } = new();
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string EvalMetric { get; set; } = "Text-based metrics";
    public double TotalCost { get; set; } = 0;
    public string GenerationStatus { get; set; } = "idle";
    public string MetricsGenerationStatus { get; set; } = "idle";
    public Dictionary<string, Dictionary<string, double>> Metrics { get; set; } = new();
    public Dictionary<string, ModelExperimentResults> ModelResults { get; set; } = new();
}

public class TaskDataSetModel
{
    public string DataSetId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int ModelOutputIndex { get; set; } = 0;
    public string GeneratedOutputKey { get; set; } = string.Empty;
    public bool IsGroundTruth { get; set; } = false;
} 