using System.Collections.Generic;

namespace GameDemo.DataConfig.Planning
{
    public sealed class PlanningConfigBatchReport
    {
        readonly List<ConfigLoadReport> _reports = new List<ConfigLoadReport>();

        public IReadOnlyList<ConfigLoadReport> Reports => _reports;
        public bool Success { get; private set; } = true;

        public void Add(ConfigLoadReport report)
        {
            _reports.Add(report);
            if (report == null || !report.Success)
            {
                Success = false;
            }
        }
    }
}

