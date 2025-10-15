using System.Text.Json.Serialization;

namespace MedBench.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExperimentStatus
{
    Draft,
    InProgress,
    Completed,
    Cancelled
} 