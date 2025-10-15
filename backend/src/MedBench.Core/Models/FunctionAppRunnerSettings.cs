namespace MedBench.Core.Models
{
    /// <summary>
    /// Constants for AzureFunctionAppRunner settings keys to provide type safety and discoverability.
    /// </summary>
    public static class FunctionAppRunnerSettings
    {
        public const string ClinicalTaskId = "ClinicalTaskId";
        public const string FunctionAppType = "FunctionAppType";
        public const string TimeoutSeconds = "TimeoutSeconds";
        public const string StorageConnectionString = "StorageConnectionString";
    }
}
