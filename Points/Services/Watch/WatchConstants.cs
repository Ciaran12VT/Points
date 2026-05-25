namespace Points.Services.Watch;

public static class WatchConstants
{
    public const int SchemaVersion = 1;
    public const int MaxShortcutCount = 9;

    public const string PhoneCapability = "points_phone_v1";
    public const string WatchCapability = "points_watch_v1";

    public const string SnapshotPath = "/points/watch-summary";
    public const string CommandPath = "/points/watch-command";
    public const string CommandAckPath = "/points/watch-command-ack";
    public const string EventPathPrefix = "/points/watch-events/";

    public const string SnapshotJsonKey = "snapshotJson";
    public const string EventJsonKey = "eventJson";
    public const string UpdatedAtMillisKey = "updatedAtMillis";

    public const string ToggleActiveAction = "toggleActive";
    public const string RecordSpendAction = "recordSpend";
    public const string CommitStepRepsAction = "commitStepReps";

    public static string ToWatchCardId(long phoneCardId) => $"card_{phoneCardId}";
    public static string ToWatchStepId(int phoneStepId) => $"step_{phoneStepId}";

    public static bool TryParseWatchCardId(string? watchCardId, out long phoneCardId)
    {
        phoneCardId = 0;

        if (string.IsNullOrWhiteSpace(watchCardId))
            return false;

        var value = watchCardId.Trim();
        if (value.StartsWith("card_", StringComparison.OrdinalIgnoreCase))
            value = value[5..];

        return long.TryParse(value, out phoneCardId) && phoneCardId > 0;
    }

    public static bool TryParseWatchStepId(string? watchStepId, out int phoneStepId)
    {
        phoneStepId = 0;

        if (string.IsNullOrWhiteSpace(watchStepId))
            return false;

        var value = watchStepId.Trim();
        if (value.StartsWith("step_", StringComparison.OrdinalIgnoreCase))
            value = value[5..];

        return int.TryParse(value, out phoneStepId) && phoneStepId > 0;
    }
}
