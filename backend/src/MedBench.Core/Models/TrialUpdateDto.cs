namespace MedBench.Core.Models;

public class TrialUpdateDto
{
    // Only include fields that can be updated by the client. All are optional.
    public string? Status { get; set; }
    public TrialResponse? Response { get; set; }
    public List<TrialFlag>? Flags { get; set; }
    public List<EvalQuestion>? Questions { get; set; }
    public List<BoundingBox>? BoundingBoxes { get; set; }
}
