using Points.Services.Backup;
using Xunit;

namespace Points.Tests.AppBackup;

public sealed class ScheduledBackupLogStoreTests
{
    [Fact]
    public async Task AppendAsync_AndGetRecentAsync_ReturnsNewestValidEntries()
    {
        using var temp = new TempFileScope();
        var store = new JsonLinesScheduledBackupLogStore(temp.Path);

        await store.AppendAsync(NewEntry(1, ScheduledBackupRunStatus.Success));
        await File.AppendAllTextAsync(temp.Path, "not-json" + Environment.NewLine);
        await store.AppendAsync(NewEntry(2, ScheduledBackupRunStatus.Failed));

        var entries = await store.GetRecentAsync(2);

        Assert.Collection(
            entries,
            first =>
            {
                Assert.Equal(ScheduledBackupRunStatus.Failed, first.Status);
                Assert.Equal(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc), first.StartedAtUtc);
            },
            second => Assert.Equal(ScheduledBackupRunStatus.Success, second.Status));
    }

    [Fact]
    public async Task PruneAsync_KeepsNewestEntries()
    {
        using var temp = new TempFileScope();
        var store = new JsonLinesScheduledBackupLogStore(temp.Path);

        await store.AppendAsync(NewEntry(1, ScheduledBackupRunStatus.Success));
        await store.AppendAsync(NewEntry(2, ScheduledBackupRunStatus.Success));
        await store.AppendAsync(NewEntry(3, ScheduledBackupRunStatus.Success));

        await store.PruneAsync(2);
        var entries = await store.GetRecentAsync(10);

        Assert.Equal(
            new[]
            {
                new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc)
            },
            entries.Select(x => x.StartedAtUtc).ToArray());
    }

    private static ScheduledBackupLogEntry NewEntry(int day, ScheduledBackupRunStatus status)
    {
        return new ScheduledBackupLogEntry
        {
            RunId = Guid.NewGuid(),
            StartedAtUtc = new DateTime(2026, 5, day, 0, 0, 0, DateTimeKind.Utc),
            CompletedAtUtc = new DateTime(2026, 5, day, 0, 1, 0, DateTimeKind.Utc),
            Status = status,
            DestinationType = ScheduledBackupDestinationType.GoogleDrive,
            DestinationDisplayName = "Google Drive",
            FileName = $"points_backup_2026050{day}.zip",
            Bytes = 1024,
            ResourceKeys = new List<string> { "database" },
            ErrorCode = status == ScheduledBackupRunStatus.Failed ? "NetworkUnavailable" : null
        };
    }

    private sealed class TempFileScope : IDisposable
    {
        private readonly string _directory;

        public TempFileScope()
        {
            _directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"PointsScheduledBackupLogStoreTests-{Guid.NewGuid():N}");
            Path = System.IO.Path.Combine(_directory, "backup_automation.log.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                    Directory.Delete(_directory, recursive: true);
            }
            catch
            {
                // Test cleanup only.
            }
        }
    }
}
