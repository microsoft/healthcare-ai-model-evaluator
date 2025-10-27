using System.Runtime.Serialization;

namespace MedBench.Core.Models;

public enum EvalMetric
{
    All,
    [EnumMember(Value = "Text-based metrics")]
    TextBasedMetrics,
    [EnumMember(Value = "Image-based metrics")]
    ImageBasedMetrics,
    [EnumMember(Value = "Accuracy metrics")]
    AccuracyMetrics,
    [EnumMember(Value = "Safety metrics")]
    SafetyMetrics,
    [EnumMember(Value = "Bias metrics")]
    BiasMetrics
}