namespace Points.Services.Sqlite
{
    public sealed partial class LockRepository
    {
        private sealed class LockScheduleRow
        {
            public long ScheduleId { get; set; }
            public long LockId { get; set; }
            public string FrequencyType { get; set; } = "";
            public int FrequencyValue { get; set; }
            public string FromDateTime { get; set; } = "";
            public string? ToDateTime { get; set; }
        }
    }
}