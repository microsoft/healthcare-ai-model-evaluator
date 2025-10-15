using System;

namespace MedBench.Core.DTOs
{
    public class ModelRanking
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double EloScore { get; set; }
        public double AverageRating { get; set; }
        public double CorrectScore { get; set; }
        public double ValidationTime { get; set; } 
        public Dictionary<string, ModelExperimentResults> ExperimentResultsByMetric { get; set; } = new();

        public Dictionary<string, ModelExperimentResults> RollingResultsByMetric { get; set; } = new();
    }
} 