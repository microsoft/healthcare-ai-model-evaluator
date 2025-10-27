namespace MedBench.Core.Models;

public class TrialStats
{
    public int PendingCount { get; set; }
    public int CompletedCount { get; set; }
    public double AverageConcordance { get; set; }
    public double TotalTimeSeconds { get; set; }
    public List<string> ExperimentIds { get; set; } = new List<string>();
    public string? TestScenarioId { get; set; }
} 