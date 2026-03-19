namespace Points.Services.Sqlite
{
    public sealed partial class ReportRepository
    {
        private sealed class ReportRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string SQLQuery { get; set; } = "";
            public string? LastRunOn { get; set; }
            public int EligibleForAchievment { get; set; }
        }
    }
}