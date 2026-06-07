using Points.Models.Watch;

namespace Points.Services.Watch;

public interface IWatchBridge
{
    Task StartAsync(CancellationToken ct = default);
    Task PublishSnapshotAsync(string snapshotJson, CancellationToken ct = default);
}

public interface IWatchSnapshotBuilder
{
    Task<WatchSummarySnapshot> BuildSnapshotAsync(CancellationToken ct = default);
    Task<string> BuildSnapshotJsonAsync(CancellationToken ct = default);
}

public interface IWatchSnapshotPublishService
{
    Task RequestPublishAsync(bool force = false, CancellationToken ct = default);
}

public interface IWatchCommandProcessor
{
    Task<WatchCommandResult> ProcessCommandJsonAsync(string commandJson, CancellationToken ct = default);
}

public interface IWatchShortcutSettingsService
{
    Task<IReadOnlyList<WatchShortcutCandidate>> GetCandidatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetSelectedCardIdsAsync(CancellationToken ct = default);
    Task SaveSelectedCardIdsAsync(IReadOnlyList<long> cardIds, CancellationToken ct = default);
}

public interface IWatchEventStore
{
    Task<bool> TryBeginProcessingAsync(string eventId, string baseSnapshotId, string createdAtUtc, CancellationToken ct = default);
    Task MarkProcessedAsync(string eventId, string status, string? message, CancellationToken ct = default);
}
