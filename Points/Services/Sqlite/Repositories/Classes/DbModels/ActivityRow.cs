namespace Points.Services.Sqlite
{
    public sealed partial class ActivityRepository
    {
        private sealed class ActivityRow
        {
            public int ActivityID { get; set; }
            public long CardID { get; set; }
            public string Start { get; set; } = string.Empty;
            public string? End { get; set; }
            public string ValueRateName { get; set; } = string.Empty;
            public double ValuePerMinute { get; set; }
        }
    }
}