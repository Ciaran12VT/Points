using System.Text.Json;
using System.Text.Json.Serialization;

namespace Points.Models.Watch;

public sealed class WatchSummarySnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string SnapshotId { get; set; } = "";
    public string GeneratedAtUtc { get; set; } = "";
    public string LocalNow { get; set; } = "";
    public string Timezone { get; set; } = "";
    public WatchGlobalSummary Global { get; set; } = new();
    public WatchNavigationSummary WatchNavigation { get; set; } = new();
    public List<WatchCardSummary> Cards { get; set; } = new();
    public List<WatchBudgetCardSummary> BudgetCards { get; set; } = new();
}

public sealed class WatchGlobalSummary
{
    public double Score { get; set; }
    public string DisplayText { get; set; } = "";
    public string Tone { get; set; } = "neutral";
    public WatchActiveCardSummary? ActiveCard { get; set; }
    public WatchNotificationSummary? Notification { get; set; }
}

public sealed class WatchActiveCardSummary
{
    public string CardId { get; set; } = "";
    public long PhoneCardId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Tone { get; set; } = "neutral";
    public bool ManageableOnWatch { get; set; }
}

public sealed class WatchNotificationSummary
{
    public bool Visible { get; set; }
    public string Text { get; set; } = "";
    public string? TargetCardId { get; set; }
}

public sealed class WatchNavigationSummary
{
    public string? SelectedCardId { get; set; }
    public List<string> RadialMenuCardIds { get; set; } = new();
}

public sealed class WatchCardSummary
{
    public string CardId { get; set; } = "";
    public long PhoneCardId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string IconKey { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public string Tone { get; set; } = "neutral";
    public WatchValueSummary CurrentValue { get; set; } = new();
    public WatchActiveSessionSummary? ActiveSession { get; set; }
    public List<WatchStepSummary> Steps { get; set; } = new();
    public List<string> SupportedActions { get; set; } = new();
}

public sealed class WatchValueSummary
{
    public double Points { get; set; }
    public string DisplayText { get; set; } = "";
}

public sealed class WatchActiveSessionSummary
{
    public string StartedAtUtc { get; set; } = "";
    public long ElapsedSeconds { get; set; }
    public string DisplayText { get; set; } = "";
    public string RateName { get; set; } = "Base Rate";
    public double ValuePerMinute { get; set; }
}

public sealed class WatchStepSummary
{
    public string StepId { get; set; } = "";
    public int PhoneStepId { get; set; }
    public string Title { get; set; } = "";
    public int RepCount { get; set; }
    public double StepValue { get; set; }
    public bool CanIncrement { get; set; }
    public bool CanDecrement { get; set; }
}

public sealed class WatchBudgetCardSummary
{
    public string CardId { get; set; } = "";
    public long PhoneCardId { get; set; }
    public string Kind { get; set; } = "budget";
    public string Title { get; set; } = "";
    public string IconKey { get; set; } = "";
    public int DisplayOrder { get; set; }
    public string Currency { get; set; } = "";
    public double Balance { get; set; }
    public string BalanceDisplayText { get; set; } = "";
    public double PercentRemaining { get; set; }
    public string PercentDisplayText { get; set; } = "";
    public string Tone { get; set; } = "neutral";
    public double ExchangeRate { get; set; }
    public bool CashInEnabled { get; set; }
    public WatchTopUpSummary? NextTopUp { get; set; }
    public List<string> SupportedActions { get; set; } = new();
}

public sealed class WatchTopUpSummary
{
    public string AtLocal { get; set; } = "";
    public double Amount { get; set; }
    public long CountdownSeconds { get; set; }
    public string CountdownDisplayText { get; set; } = "";
}

public sealed class WatchCommandEvent
{
    public int SchemaVersion { get; set; } = 1;
    public string EventId { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
    public string BaseSnapshotId { get; set; } = "";
    public string ActionName { get; set; } = "";
    public string CardId { get; set; } = "";
    public Dictionary<string, string> Payload { get; set; } = new();
}

public sealed class WatchCommandResult
{
    public bool Accepted { get; init; }
    public bool Duplicate { get; init; }
    public string Message { get; init; } = "";

    public static WatchCommandResult Success(string message = "Accepted.") =>
        new() { Accepted = true, Message = message };

    public static WatchCommandResult Rejected(string message) =>
        new() { Accepted = false, Message = message };

    public static WatchCommandResult IgnoredDuplicate(string message = "Duplicate event ignored.") =>
        new() { Accepted = false, Duplicate = true, Message = message };
}

public sealed class WatchShortcutCandidate
{
    public long CardId { get; set; }
    public string WatchCardId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";
    public string IconChar { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsSelected { get; set; }
}

public static class WatchJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
