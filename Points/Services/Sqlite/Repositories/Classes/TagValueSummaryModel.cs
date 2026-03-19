namespace Points.Services.Sqlite.Repositories.Classes
{
    public sealed class TagValueSummaryModel
    {
        public double CurrentValue { get; init; }
        public double CurrentTotalActiveTimeInSeconds { get; init; }
    }
}