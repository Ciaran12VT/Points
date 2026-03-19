namespace Points.Services.Sqlite
{
    public sealed partial class LockRepository
    {
        private sealed class LockRow
        {
            public long LockId { get; set; }
            public int LockNumber { get; set; }
            public long CardId { get; set; }
            public string TimeWindowStart { get; set; } = "";
            public string TimeWindowEnd { get; set; } = "";
        }
    }
}