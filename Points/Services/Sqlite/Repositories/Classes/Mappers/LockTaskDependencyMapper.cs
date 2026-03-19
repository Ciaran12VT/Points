using Points.Models;

namespace Points.Services.Sqlite
{
    public sealed partial class LockRepository
    {
        private static class LockTaskDependencyMapper
        {
            public static LockTaskDependencyModel ToDomain(LockTaskDependencyRow row)
            {
                return new LockTaskDependencyModel
                {
                    LockTaskDependencyId = row.LockTaskDependencyId,
                    LockId = row.LockId,
                    TaskDependencyCardId = row.TaskDependencyCardId,
                    MetricType = (LockDependencyMetricType)row.MetricType,
                    TimeScope = (TimeScope)row.TimeScope,
                    GoalValue = row.GoalValue,
                    GoalValence = (GoalValence)row.GoalValence
                };
            }
        }
    }
}