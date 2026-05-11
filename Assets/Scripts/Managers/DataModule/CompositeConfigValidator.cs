using System.Collections.Generic;

namespace GameDemo.DataConfig
{
    public sealed class CompositeConfigValidator<T> : IConfigValidator<T> where T : IConfigRecord
    {
        readonly List<IConfigValidator<T>> _validators = new List<IConfigValidator<T>>();

        public CompositeConfigValidator<T> Add(IConfigValidator<T> validator)
        {
            if (validator != null)
            {
                _validators.Add(validator);
            }

            return this;
        }

        public ConfigValidationReport Validate(IReadOnlyList<T> records)
        {
            var allIssues = new List<ConfigIssue>();
            for (int i = 0; i < _validators.Count; i++)
            {
                ConfigValidationReport report = _validators[i].Validate(records);
                if (report?.Issues == null)
                {
                    continue;
                }

                allIssues.AddRange(report.Issues);
            }

            return new ConfigValidationReport(allIssues);
        }
    }
}
