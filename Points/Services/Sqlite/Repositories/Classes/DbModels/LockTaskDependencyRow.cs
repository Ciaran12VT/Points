namespace Points.Services.Sqlite
{
    public sealed partial class LockRepository
    {
        private sealed class LockTaskDependencyRow
        {
            public long LockTaskDependencyId { get; set; }
            public long LockId { get; set; }
            public long TaskDependencyCardId { get; set; }
            public int MetricType { get; set; }
            public int TimeScope { get; set; }
            public double GoalValue { get; set; }
            public int GoalValence { get; set; }
        }
    }
}