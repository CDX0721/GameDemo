using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public sealed class ConfigValidationReport
    {
        public IReadOnlyList<ConfigIssue> Issues { get; }
        public bool HasErrors { get; }

        public ConfigValidationReport(IReadOnlyList<ConfigIssue> issues)
        {
            Issues = issues ?? new List<ConfigIssue>();

            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Level == ConfigIssueLevel.Error)
                {
                    HasErrors = true;
                    break;
                }
            }
        }
    }
}
