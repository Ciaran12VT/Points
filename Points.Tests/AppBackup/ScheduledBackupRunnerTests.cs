using Points.Models;
using Points.Services.Backup;
using Points.Services.Time;
using Xunit;

namespace Points.Tests.AppBackup;

public sealed class ScheduledBackupRunnerTests
{
    [Fact]
    public async Task RunDueAsync_DueDeviceStorageExport_CreatesLocalFileAndAdvancesSchedule()
    {
        using var temp = new TempFolderScope();
        var clock = new MutableClock(Local(2026, 5, 1, 2, 5), Utc(2026, 5, 1, 1, 5));
        var configStore = new JsonScheduledBackupConfigStore(temp.ConfigPath, clock);
        var logStore = new JsonLinesScheduledBackupLogStore(temp.LogPath);
        var packageCreator = new FakePackageCreator(temp.PackageFolder);
        var localStorage = new AppPrivateScheduledBackupLocalStorage(temp.ExportFolder, clock);
        var remoteStorage = new FakeRemoteStorage();
        var runner = new ScheduledBackupRunner(configStore, logStore, packageCreator, localStorage, remoteStorage, clock);

        await File.WriteAllTextAsync(Path.Combine(temp.ExportFolder, "points_scheduled_backup_old.zip"), "old");
        File.SetLastWriteTimeUtc(
            Path.Combine(temp.ExportFolder, "points_scheduled_backup_old.zip"),
            Utc(2026, 4, 30, 0, 0));

        await configStore.SaveAsync(new ScheduledBackupConfig
        {
            IsEnabled = true,
            Schedule = DailySchedule(),
            ResourceKeys = new List<string> { "database", "image_metadata" },
            Destination = ScheduledBackupDestinationConfig.DeviceStorage(),
            RetentionCount = 1,
            NextRunAtLocal = Local(2026, 5, 1, 2, 0)
        });

        var outcome = await runner.RunDueAsync();

        Assert.Equal(ScheduledBackupRunResult.Success, outcome.Result);
        Assert.True(File.Exists(outcome.StoredFilePath));
        Assert.Equal(new[] { "database", "image_metadata" }, packageCreator.LastResourceKeys);

        var files = Directory.GetFiles(temp.ExportFolder, "points_scheduled_backup_*.zip");
        Assert.Single(files);
        Assert.DoesNotContain(files, file => file.EndsWith("_old.zip", StringComparison.Ordinal));

        var saved = await configStore.GetAsync();
        Assert.Equal(Utc(2026, 5, 1, 1, 5), saved.LastRunCompletedAtUtc);
        Assert.Equal(Local(2026, 5, 2, 2, 0), saved.NextRunAtLocal);
        Assert.Null(saved.LastErrorCode);

        var logs = await logStore.GetRecentAsync(1);
        var log = Assert.Single(logs);
        Assert.Equal(ScheduledBackupRunStatus.Success, log.Status);
        Assert.Equal(outcome.StoredFilePath, log.FilePath);
        Assert.True(log.Bytes > 0);
    }

    [Fact]
    public async Task RunDueAsync_NotDue_DoesNotCreatePackage()
    {
        using var temp = new TempFolderScope();
        var clock = new MutableClock(Local(2026, 5, 1, 1, 0), Utc(2026, 5, 1, 0, 0));
        var configStore = new JsonScheduledBackupConfigStore(temp.ConfigPath, clock);
        var logStore = new JsonLinesScheduledBackupLogStore(temp.LogPath);
        var packageCreator = new FakePackageCreator(temp.PackageFolder);
        var runner = new ScheduledBackupRunner(
            configStore,
            logStore,
            packageCreator,
            new AppPrivateScheduledBackupLocalStorage(temp.ExportFolder, clock),
            new FakeRemoteStorage(),
            clock);

        await configStore.SaveAsync(new ScheduledBackupConfig
        {
            IsEnabled = true,
            Schedule = DailySchedule(),
            Destination = ScheduledBackupDestinationConfig.DeviceStorage(),
            NextRunAtLocal = Local(2026, 5, 1, 2, 0)
        });

        var outcome = await runner.RunDueAsync();

        Assert.Equal(ScheduledBackupRunResult.NotDue, outcome.Result);
        Assert.Equal(0, packageCreator.CreateCount);
        Assert.Empty(await logStore.GetRecentAsync(10));
    }

    [Fact]
    public async Task RunDueAsync_DueGoogleDriveExport_UploadsToRemoteStorage()
    {
        using var temp = new TempFolderScope();
        var clock = new MutableClock(Local(2026, 5, 1, 2, 5), Utc(2026, 5, 1, 1, 5));
        var configStore = new JsonScheduledBackupConfigStore(temp.ConfigPath, clock);
        var logStore = new JsonLinesScheduledBackupLogStore(temp.LogPath);
        var packageCreator = new FakePackageCreator(temp.PackageFolder);
        var remoteStorage = new FakeRemoteStorage();
        var runner = new ScheduledBackupRunner(
            configStore,
            logStore,
            packageCreator,
            new AppPrivateScheduledBackupLocalStorage(temp.ExportFolder, clock),
            remoteStorage,
            clock);

        await configStore.SaveAsync(new ScheduledBackupConfig
        {
            IsEnabled = true,
            Schedule = DailySchedule(),
            Destination = new ScheduledBackupDestinationConfig
            {
                Type = ScheduledBackupDestinationType.GoogleDrive,
                DisplayName = "Google Drive",
                GoogleDriveCredentialKey = "future-token-key"
            },
            NextRunAtLocal = Local(2026, 5, 1, 2, 0)
        });

        var outcome = await runner.RunDueAsync();

        Assert.Equal(ScheduledBackupRunResult.Success, outcome.Result);
        Assert.Equal(1, packageCreator.CreateCount);
        Assert.Equal(1, remoteStorage.StoreCount);
        Assert.Equal(1, remoteStorage.PruneCount);

        var saved = await configStore.GetAsync();
        Assert.False(saved.RequiresUserAction);
        Assert.Null(saved.LastErrorCode);
        Assert.Equal(Local(2026, 5, 2, 2, 0), saved.NextRunAtLocal);

        var log = Assert.Single(await logStore.GetRecentAsync(1));
        Assert.Equal(ScheduledBackupRunStatus.Success, log.Status);
        Assert.Equal("https://drive.google.com/file/d/uploaded/view", log.FilePath);
    }

    [Fact]
    public async Task RunDueAsync_GoogleDriveNeedsReconnect_LogsUserAction()
    {
        using var temp = new TempFolderScope();
        var clock = new MutableClock(Local(2026, 5, 1, 2, 5), Utc(2026, 5, 1, 1, 5));
        var configStore = new JsonScheduledBackupConfigStore(temp.ConfigPath, clock);
        var logStore = new JsonLinesScheduledBackupLogStore(temp.LogPath);
        var packageCreator = new FakePackageCreator(temp.PackageFolder);
        var remoteStorage = new FakeRemoteStorage
        {
            ExceptionToThrow = new ScheduledBackupRequiresUserActionException(
                "GoogleDriveReconnectRequired",
                "Reconnect Google Drive.")
        };
        var runner = new ScheduledBackupRunner(
            configStore,
            logStore,
            packageCreator,
            new AppPrivateScheduledBackupLocalStorage(temp.ExportFolder, clock),
            remoteStorage,
            clock);

        await configStore.SaveAsync(new ScheduledBackupConfig
        {
            IsEnabled = true,
            Schedule = DailySchedule(),
            Destination = new ScheduledBackupDestinationConfig
            {
                Type = ScheduledBackupDestinationType.GoogleDrive,
                DisplayName = "Google Drive",
                GoogleDriveCredentialKey = "scheduled-backup"
            },
            NextRunAtLocal = Local(2026, 5, 1, 2, 0)
        });

        var outcome = await runner.RunDueAsync();

        Assert.Equal(ScheduledBackupRunResult.RequiresUserAction, outcome.Result);

        var saved = await configStore.GetAsync();
        Assert.True(saved.RequiresUserAction);
        Assert.Equal("GoogleDriveReconnectRequired", saved.LastErrorCode);
        Assert.Null(saved.NextRunAtLocal);

        var log = Assert.Single(await logStore.GetRecentAsync(1));
        Assert.Equal(ScheduledBackupRunStatus.RequiresUserAction, log.Status);
    }

    [Fact]
    public async Task RunDueAsync_PackageCreationFails_LogsFailureAndAdvancesSchedule()
    {
        using var temp = new TempFolderScope();
        var clock = new MutableClock(Local(2026, 5, 1, 2, 5), Utc(2026, 5, 1, 1, 5));
        var configStore = new JsonScheduledBackupConfigStore(temp.ConfigPath, clock);
        var logStore = new JsonLinesScheduledBackupLogStore(temp.LogPath);
        var packageCreator = new FakePackageCreator(temp.PackageFolder)
        {
            ExceptionToThrow = new InvalidOperationException("No package for you.")
        };
        var runner = new ScheduledBackupRunner(
            configStore,
            logStore,
            packageCreator,
            new AppPrivateScheduledBackupLocalStorage(temp.ExportFolder, clock),
            new FakeRemoteStorage(),
            clock);

        await configStore.SaveAsync(new ScheduledBackupConfig
        {
            IsEnabled = true,
            Schedule = DailySchedule(),
            Destination = ScheduledBackupDestinationConfig.DeviceStorage(),
            NextRunAtLocal = Local(2026, 5, 1, 2, 0)
        });

        var outcome = await runner.RunDueAsync();

        Assert.Equal(ScheduledBackupRunResult.Failed, outcome.Result);

        var saved = await configStore.GetAsync();
        Assert.Equal("InvalidOperationException", saved.LastErrorCode);
        Assert.Equal(Local(2026, 5, 2, 2, 0), saved.NextRunAtLocal);

        var log = Assert.Single(await logStore.GetRecentAsync(1));
        Assert.Equal(ScheduledBackupRunStatus.Failed, log.Status);
        Assert.Equal("No package for you.", log.ErrorMessage);
    }

    private static ScheduledBackupSchedule DailySchedule()
    {
        return new ScheduledBackupSchedule
        {
            FrequencyType = FrequencyType.EveryDays,
            FrequencyValue = 1,
            FromDateTime = Local(2026, 5, 1, 2, 0),
            IsEnabled = true
        };
    }

    private static DateTime Local(int year, int month, int day, int hour, int minute)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }

    private sealed class FakePackageCreator : IScheduledBackupPackageCreator
    {
        private readonly string _folderPath;

        public FakePackageCreator(string folderPath)
        {
            _folderPath = folderPath;
        }

        public int CreateCount { get; private set; }
        public IReadOnlyList<string> LastResourceKeys { get; private set; } = Array.Empty<string>();
        public Exception? ExceptionToThrow { get; init; }

        public async Task<string> CreatePackageAsync(
            IEnumerable<string> resourceKeys,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            LastResourceKeys = resourceKeys.ToList();

            if (ExceptionToThrow != null)
                throw ExceptionToThrow;

            Directory.CreateDirectory(_folderPath);
            var path = Path.Combine(_folderPath, $"package-{Guid.NewGuid():N}.zip");
            await File.WriteAllTextAsync(path, "backup package", cancellationToken);
            return path;
        }
    }

    private sealed class MutableClock : IClock
    {
        public MutableClock(DateTime localNow, DateTime utcNow)
        {
            LocalNow = localNow;
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
        public DateTime LocalNow { get; set; }
        public DateTimeOffset UtcNowOffset => new(UtcNow);
    }

    private sealed class FakeRemoteStorage : IScheduledBackupRemoteStorage
    {
        public int StoreCount { get; private set; }
        public int PruneCount { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task<ScheduledBackupStoredFile> StoreAsync(
            string packagePath,
            ScheduledBackupConfig config,
            CancellationToken cancellationToken = default)
        {
            StoreCount++;

            if (ExceptionToThrow != null)
                throw ExceptionToThrow;

            return Task.FromResult(new ScheduledBackupStoredFile
            {
                FileName = "points_scheduled_backup_20260501_020500.zip",
                FilePath = "https://drive.google.com/file/d/uploaded/view",
                Bytes = new FileInfo(packagePath).Length
            });
        }

        public Task PruneAsync(
            ScheduledBackupConfig config,
            int retentionCount,
            CancellationToken cancellationToken = default)
        {
            PruneCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TempFolderScope : IDisposable
    {
        private readonly string _root;

        public TempFolderScope()
        {
            _root = Path.Combine(Path.GetTempPath(), $"PointsScheduledBackupRunnerTests-{Guid.NewGuid():N}");
            ConfigPath = Path.Combine(_root, "config", "backup_automation.json");
            LogPath = Path.Combine(_root, "logs", "backup_automation.log.jsonl");
            ExportFolder = Path.Combine(_root, "exports");
            PackageFolder = Path.Combine(_root, "packages");

            Directory.CreateDirectory(ExportFolder);
            Directory.CreateDirectory(PackageFolder);
        }

        public string ConfigPath { get; }
        public string LogPath { get; }
        public string ExportFolder { get; }
        public string PackageFolder { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Test cleanup only.
            }
        }
    }
}
