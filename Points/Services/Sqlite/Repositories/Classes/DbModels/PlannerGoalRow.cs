namespace Points.Services.Sqlite
{
    public sealed partial class PlannerRepository
    {
        private sealed class PlannerGoalRow
        {
            public long CardID { get; set; }
            public string TimeScope { get; set; } = string.Empty;
            public double GoalHrs { get; set; }
            public int Enabled { get; set; }
            public string? DeFactoStart { get; set; }
            public string? DeFactoEnd { get; set; }
        }
    }
}