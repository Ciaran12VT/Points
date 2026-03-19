namespace Points.Services.Sqlite
{
    public sealed partial class ActivityRepository
    {
        private sealed class ToggleActivityRowResult
        {
            public ActivityRow? Closed { get; init; }
            public ActivityRow? Opened { get; init; }
        }
    }
}