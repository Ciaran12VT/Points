namespace Points.Services.Sqlite
{
    public sealed partial class CardReadRepository
    {
        private sealed class CardScheduleRow
        {
            public long ScheduleId { get; set; }
            public long CardId { get; set; }
            public int IsEnabled { get; set; }
            public string? Note { get; set; }
            public int FrequencyType { get; set; }
            public int FrequencyValue { get; set; }
            public string FromDateTime { get; set; } = string.Empty;
            public string? ToDateTime { get; set; }
        }
    }
}