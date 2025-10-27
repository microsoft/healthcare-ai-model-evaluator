namespace MedBench.Core.Models;

public class TrialUpdateRequest
{
    public Trial Trial { get; set; } = new();
    public float? TimeSpent { get; set; }
} 