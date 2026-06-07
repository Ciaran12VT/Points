using Points.Services.Persistence;
using Points.Services.Sqlite;
using Points.Services.Time;

namespace Points.Services.Watch;

public sealed class SqliteWatchEventStore : IWatchEventStore
{
    private readonly ISqliteConnectionContext _context;
    private readonly IClock _clock;

    public SqliteWatchEventStore(ISqliteConnectionContext context, IClock clock)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<bool> TryBeginProcessingAsync(
        string eventId,
        string baseSnapshotId,
        string createdAtUtc,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        await _context.InitializeAsync();

        var inserted = 0;
        await _context.RunInTransactionAsync(conn =>
        {
            inserted = conn.Execute(
                @"INSERT OR IGNORE INTO WatchProcessedEvent
                    (EventId, BaseSnapshotId, CreatedAtUtc, ProcessedAtUtc, Status, Message)
                  VALUES (?, ?, ?, ?, ?, ?);",
                eventId.Trim(),
                baseSnapshotId ?? "",
                createdAtUtc ?? "",
                StrictTimeSerializer.SerializeUtcInstant(_clock.UtcNow),
                "Processing",
                "");
        });

        return inserted > 0;
    }

    public async Task MarkProcessedAsync(
        string eventId,
        string status,
        string? message,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(eventId))
            return;

        await _context.InitializeAsync();
        await _context.Db.ExecuteAsync(
            @"UPDATE WatchProcessedEvent
              SET ProcessedAtUtc = ?,
                  Status = ?,
                  Message = ?
              WHERE EventId = ?;",
            StrictTimeSerializer.SerializeUtcInstant(_clock.UtcNow),
            string.IsNullOrWhiteSpace(status) ? "Processed" : status,
            message ?? "",
            eventId.Trim());
    }
}
