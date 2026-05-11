using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    /// <summary>
    /// Shared validation rules: null record, empty id, duplicate id.
    /// </summary>
    public sealed class DefaultConfigValidator<T> : IConfigValidator<T> where T : IConfigRecord
    {
        public ConfigValidationReport Validate(IReadOnlyList<T> records)
        {
            var issues = new List<ConfigIssue>();

            if (records == null)
            {
                issues.Add(new ConfigIssue(
                    ConfigIssueLevel.Error,
                    "config.records.null",
                    "Record list is null."));
                return new ConfigValidationReport(issues);
            }

            var seen = new HashSet<string>();
            for (int i = 0; i < records.Count; i++)
            {
                T record = records[i];
                if (record == null)
                {
                    issues.Add(new ConfigIssue(
                        ConfigIssueLevel.Error,
                        "config.record.null",
                        $"Record at index {i} is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.Id))
                {
                    issues.Add(new ConfigIssue(
                        ConfigIssueLevel.Error,
                        "config.record.id.empty",
                        $"Record at index {i} has an empty id."));
                    continue;
                }

                if (!seen.Add(record.Id))
                {
                    issues.Add(new ConfigIssue(
                        ConfigIssueLevel.Error,
                        "config.record.id.duplicate",
                        $"Duplicate id detected: \"{record.Id}\"."));
                }
            }

            return new ConfigValidationReport(issues);
        }
    }
}
