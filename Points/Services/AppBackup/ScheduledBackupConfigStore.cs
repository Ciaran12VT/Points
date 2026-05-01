using Points.Global;
using Points.Models;
using Points.Services.Scheduling;
using Points.Services.Time;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Points.Services.Backup
{
    public interface IScheduledBackupConfigStore
    {
        Task<ScheduledBackupConfig> GetAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(ScheduledBackupConfig config, CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }

    public sealed class JsonScheduledBackupConfigStore : IScheduledBackupConfigStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _configPath;
        private readonly IClock _clock;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public JsonScheduledBackupConfigStore()
            : this(AppPaths.BackupAutomationConfigPath, new SystemClock())
        {
        }

        public JsonScheduledBackupConfigStore(IClock clock)
            : this(AppPaths.BackupAutomationConfigPath, clock)
        {
        }

        public JsonScheduledBackupConfigStore(string configPath)
            : this(configPath, new SystemClock())
        {
        }

        public JsonScheduledBackupConfigStore(string configPath, IClock clock)
        {
            _configPath = string.IsNullOrWhiteSpace(configPath)
                ? throw new ArgumentException("Config path is required.", nameof(configPath))
                : configPath;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<ScheduledBackupConfig> GetAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_configPath))
                    return ScheduledBackupConfig.DisabledDefault();

                try
                {
                    await using var stream = File.OpenRead(_configPath);
                    var config = await JsonSerializer.DeserializeAsync<ScheduledBackupConfig>(
                        stream,
                        JsonOptions,
                        cancellationToken);

                    return Normalize(config);
                }
                catch (JsonException ex)
                {
                    QuarantineInvalidConfig(ex);
                    return ScheduledBackupConfig.DisabledDefault();
                }
                catch (NotSupportedException ex)
                {
                    QuarantineInvalidConfig(ex);
                    return ScheduledBackupConfig.DisabledDefault();
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task SaveAsync(ScheduledBackupConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            await _gate.WaitAsync(cancellationToken);
            try
            {
                var normalized = Normalize(config);
                var directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = $"{_configPath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await using (var stream = File.Create(tempPath))
                    {
                        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
                    }

                    File.Move(tempPath, _configPath, overwrite: true);
                }
                finally
                {
                    TryDelete(tempPath);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(_configPath))
                    File.Delete(_configPath);
            }
            finally
            {
                _gate.Release();
            }
        }

        private static ScheduledBackupConfig Normalize(ScheduledBackupConfig? config)
        {
            config ??= ScheduledBackupConfig.DisabledDefault();

            if (config.Version <= 0)
                config.Version = ScheduledBackupConfig.CurrentVersion;

            if (config.Version > ScheduledBackupConfig.CurrentVersion)
            {
                config.IsEnabled = false;
                config.RequiresUserAction = true;
                config.LastErrorCode = "UnsupportedConfigVersion";
                config.LastErrorMessage = "Automatic export settings were created by a newer version of Points.";
            }

            config.Schedule ??= ScheduledBackupSchedule.Default();
            config.Schedule.FromDateTime = WallClockScheduleTime.NormalizeLocal(config.Schedule.FromDateTime);
            config.Schedule.ToDateTime = WallClockScheduleTime.NormalizeLocal(config.Schedule.ToDateTime);

            if (config.Schedule.FrequencyType == FrequencyType.EveryDays && config.Schedule.FrequencyValue <= 0)
                config.Schedule.FrequencyValue = 1;

            config.Destination ??= ScheduledBackupDestinationConfig.DeviceStorage();
            if (string.IsNullOrWhiteSpace(config.Destination.DisplayName))
            {
                config.Destination.DisplayName = config.Destination.Type == ScheduledBackupDestinationType.GoogleDrive
                    ? "Google Drive"
                    : "App exports folder";
            }

            config.ResourceKeys = config.ResourceKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (config.ResourceKeys.Count == 0)
                config.ResourceKeys.Add("database");

            if (config.RetentionCount < 1)
                config.RetentionCount = 1;

            config.NextRunAtLocal = WallClockScheduleTime.NormalizeLocal(config.NextRunAtLocal);

            return config;
        }

        private void QuarantineInvalidConfig(Exception ex)
        {
            try
            {
                if (!File.Exists(_configPath))
                    return;

                var quarantinePath = $"{_configPath}.corrupt-{_clock.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
                File.Move(_configPath, quarantinePath);
                System.Diagnostics.Debug.WriteLine($"Invalid scheduled backup config was quarantined: {ex}");
            }
            catch
            {
                // If quarantine fails, still let the app continue with disabled defaults.
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort cleanup for interrupted config writes.
            }
        }
    }
}
