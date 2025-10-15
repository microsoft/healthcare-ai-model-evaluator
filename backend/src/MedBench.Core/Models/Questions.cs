using System.Globalization;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace MedBench.Core.Models;

public class EvalQuestionOption
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Value { get; set; } = "";
}

public class EvalQuestion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public List<EvalQuestionOption> Options { get; set; } = new List<EvalQuestionOption>();
    public string? EvalMetric { get; set; }
    public string? Response { get; set; } = string.Empty;
}