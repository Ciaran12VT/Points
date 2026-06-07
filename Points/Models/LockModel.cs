using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class LockModel
    {
        public long LockId { get; set; }
        public int LockNumber { get; set; }
        public long CardId { get; set; }

        public TimeOnly TimeWindowStart { get; set; }
        public TimeOnly TimeWindowEnd { get; set; }

        public List<LockScheduleModel> Schedules { get; set; } = new();
        public List<LockTaskDependencyModel> Dependencies { get; set; } = new();
    }

    public sealed class LockScheduleModel : IScheduleModel
    {
        public long ScheduleId { get; set; }
        public long LockId { get; set; }

        public FrequencyType FrequencyType { get; set; }
        public int FrequencyValue { get; set; }

        public DateTime FromDateTime { get; set; }
        public DateTime? ToDateTime { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string? Note { get; set; } = null;
    }

    public sealed class LockTaskDependencyModel
    {
        public long LockTaskDependencyId { get; set; }
        public long LockId { get; set; }

        public long TaskDependencyCardId { get; set; }
        public LockDependencyMetricType MetricType { get; set; }
        public TimeScope TimeScope { get; set; }

        public double TargetValue { get; set; }

        public TargetValence TargetValence { get; set; } = TargetValence.MustBeGreaterThan;
    }

    public enum LockDependencyMetricType
    {
        ActiveTime = 0,
        Points = 1
    }

    public enum TargetValence
    {
        MustBeGreaterThan = 0,
        MustBeLessThan = 1
    }
}
