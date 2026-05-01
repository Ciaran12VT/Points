using Points.Models;
using Points.Services.Backup;
using Xunit;

namespace Points.Tests.AppBackup;

public sealed class ScheduledBackupConfigStoreTests
{
    [Fact]
    public async Task GetAsync_MissingConfig_ReturnsDisabledDefault()
    {
        using var temp = new TempFileScope();
        var store = new JsonScheduledBackupConfigStore(temp.Path);

        var config = await store.GetAsync();

        Assert.False(config.IsEnabled);
        Assert.Equal(new[] { "database" }, config.ResourceKeys);
        Assert.Equal(ScheduledBackupDestinationType.DeviceStorage, config.Destination.Type);
        Assert.Equal(7, config.RetentionCount);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsNormalizedConfig()
    {
        using var temp = new TempFileScope();
        var store = new JsonScheduledBackupConfigStore(temp.Path);
        var config = new ScheduledBackupConfig
        {
            IsEnabled = true,
            RetentionCount = 0,
            ResourceKeys = new List<string> { "database", "database", "", "image_metadata" },
            Schedule = new ScheduledBackupSchedule
            {
                FrequencyType = FrequencyType.EveryDays,
                FrequencyValue = 0,
                FromDateTime = new DateTime(2026, 5, 1, 2, 0, 0, DateTimeKind.Local)
            },
            Destination = new ScheduledBackupDestinationConfig
            {
                Type = ScheduledBackupDestinationType.GoogleDrive,
                DisplayName = "",
                GoogleDriveFolderId = "folder-1",
                GoogleDriveCredentialKey = "secure-key"
            }
        };

        await store.SaveAsync(config);
        var saved = await store.GetAsync();

        Assert.True(saved.IsEnabled);
        Assert.Equal(new[] { "database", "image_metadata" }, saved.ResourceKeys);
        Assert.Equal(1, saved.RetentionCount);
        Assert.Equal(1, saved.Schedule.FrequencyValue);
        Assert.Equal(DateTimeKind.Unspecified, saved.Schedule.FromDateTime.Kind);
        Assert.Equal("Google Drive", saved.Destination.DisplayName);
        Assert.Equal("folder-1", saved.Destination.GoogleDriveFolderId);
        Assert.Equal("secure-key", saved.Destination.GoogleDriveCredentialKey);
    }

    [Fact]
    public async Task GetAsync_CorruptConfig_QuarantinesFileAndReturnsDisabledDefault()
    {
        using var temp = new TempFileScope();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(temp.Path)!);
        await File.WriteAllTextAsync(temp.Path, "{ this is not json");

        var store = new JsonScheduledBackupConfigStore(temp.Path);

        var config = await store.GetAsync();

        Assert.False(config.IsEnabled);
        Assert.False(File.Exists(temp.Path));
        Assert.NotEmpty(Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(temp.Path)!, "*.corrupt-*"));
    }

    private sealed class TempFileScope : IDisposable
    {
        private readonly string _directory;

        public TempFileScope()
        {
            _directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"PointsScheduledBackupConfigStoreTests-{Guid.NewGuid():N}");
            Path = System.IO.Path.Combine(_directory, "backup_automation.json");
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
