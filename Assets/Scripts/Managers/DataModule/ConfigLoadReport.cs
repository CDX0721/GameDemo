using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public sealed class ConfigLoadReport
    {
        public bool Success { get; }
        public string ResourcePath { get; }
        public int LoadedCount { get; }
        public IReadOnlyList<ConfigIssue> Issues { get; }

        public ConfigLoadReport(bool success, string resourcePath, int loadedCount, IReadOnlyList<ConfigIssue> issues)
        {
            Success = success;
            ResourcePath = resourcePath;
            LoadedCount = loadedCount;
            Issues = issues ?? new List<ConfigIssue>();
        }

        public static ConfigLoadReport Fail(string resourcePath, string code, string message)
        {
            return new ConfigLoadReport(
                false,
                resourcePath,
                0,
                new List<ConfigIssue>
                {
                    new ConfigIssue(ConfigIssueLevel.Error, code, message)
                });
        }
    }
}
