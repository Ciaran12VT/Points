using Points.Services.Scheduling;
using Points.Services.Time;

namespace Points.Services.Backup
{
    public enum ScheduledBackupRunResult
    {
        Disabled,
        NotDue,
        Busy,
        Success,
        Failed,
        RequiresUserAction
    }

    public sealed class ScheduledBackupRunOutcome
    {
        public ScheduledBackupRunResult Result { get; init; }
        public ScheduledBackupLogEntry? LogEntry { get; init; }
        public string? StoredFilePath { get; init; }
    }

    public interface IScheduledBackupRunner
    {
        Task<ScheduledBackupRunOutcome> RunDueAsync(CancellationToken cancellationToken = default);
    }

    public interface IScheduledBackupPackageCreator
    {
        Task<string> CreatePackageAsync(
            IEnumerable<string> resourceKeys,
            CancellationToken cancellationToken = default);
    }

    public sealed class ScheduledBackupRunner : IScheduledBackupRunner
    {
        private const int MaxLogEntries = 50;

        private readonly IScheduledBackupConfigStore _configStore;
        private readonly IScheduledBackupLogStore _logStore;
        private readonly IScheduledBackupPackageCreator _packageCreator;
        private readonly IScheduledBackupLocalStorage _localStorage;
        private readonly IScheduledBackupRemoteStorage _remoteStorage;
        private readonly IClock _clock;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public ScheduledBackupRunner(
            IScheduledBackupConfigStore configStore,
            IScheduledBackupLogStore logStore,
            IScheduledBackupPackageCreator packageCreator,
            IScheduledBackupLocalStorage localStorage,
            IScheduledBackupRemoteStorage remoteStorage,
            IClock clock)
        {
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _packageCreator = packageCreator ?? throw new ArgumentNullException(nameof(packageCreator));
            _localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
            _remoteStorage = remoteStorage ?? throw new ArgumentNullException(nameof(remoteStorage));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<ScheduledBackupRunOutcome> RunDueAsync(CancellationToken cancellationToken = default)
        {
            if (!await _gate.WaitAsync(0, cancellationToken))
                return new ScheduledBackupRunOutcome { Result = ScheduledBackupRunResult.Busy };

            try
            {
                var config = await _configStore.GetAsync(cancellationToken);
                var nowLocal = WallClockScheduleTime.NormalizeLocal(_clock.LocalNow);

                if (!config.IsEnabled || config.Schedule?.IsEnabled != true)
                    return new ScheduledBackupRunOutcome { Result = ScheduledBackupRunResult.Disabled };

                if (config.RequiresUserAction)
                    return new ScheduledBackupRunOutcome { Result = ScheduledBackupRunResult.RequiresUserAction };

                var nextRun = WallClockScheduleTime.NormalizeLocal(config.NextRunAtLocal);
                if (!nextRun.HasValue)
                {
                    config.NextRunAtLocal = CardScheduleOccurrenceCalculator.GetNextOccurrence(config.Schedule, nowLocal);
                    await _configStore.SaveAsync(config, cancellationToken);
                    return new ScheduledBackupRunOutcome { Result = ScheduledBackupRunResult.NotDue };
                }

                if (nextRun.Value > nowLocal)
                    return new ScheduledBackupRunOutcome { Result = ScheduledBackupRunResult.NotDue };

                return config.Destination.Type == ScheduledBackupDestinationType.GoogleDrive
                    ? await RunGoogleDriveExportAsync(config, nowLocal, cancellationToken)
                    : await RunLocalExportAsync(config, nowLocal, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<ScheduledBackupRunOutcome> RunLocalExportAsync(
            ScheduledBackupConfig config,
            DateTime startedAtLocal,
            CancellationToken cancellationToken)
        {
            var startedAtUtc = _clock.UtcNow;
            var entry = CreateBaseLogEntry(config, startedAtUtc);
            string? packagePath = null;

            config.LastRunStartedAtUtc = startedAtUtc;
            config.LastRunCompletedAtUtc = null;
            config.LastErrorCode = null;
            config.LastErrorMessage = null;
            await _configStore.SaveAsync(config, cancellationToken);

            try
            {
                packagePath = await _packageCreator.CreatePackageAsync(config.ResourceKeys, cancellationToken);
                var stored = await _localStorage.StoreAsync(packagePath, config, cancellationToken);
                await _localStorage.PruneAsync(config.RetentionCount, cancellationToken);

                var completedAtUtc = _clock.UtcNow;
                entry.Status = ScheduledBackupRunStatus.Success;
                entry.CompletedAtUtc = completedAtUtc;
                entry.FileName = stored.FileName;
                entry.FilePath = stored.FilePath;
                entry.Bytes = stored.Bytes;

                config.LastRunCompletedAtUtc = completedAtUtc;
                config.NextRunAtLocal = CardScheduleOccurrenceCalculator.GetNextOccurrence(
                    config.Schedule,
                    WallClockScheduleTime.NormalizeLocal(_clock.LocalNow));

                await _logStore.AppendAsync(entry, cancellationToken);
                await _logStore.PruneAsync(MaxLogEntries, cancellationToken);
                await _configStore.SaveAsync(config, cancellationToken);

                return new ScheduledBackupRunOutcome
                {
                    Result = ScheduledBackupRunResult.Success,
                    LogEntry = entry,
                    StoredFilePath = stored.FilePath
                };
            }
            catch (ScheduledBackupRequiresUserActionException ex)
            {
                return await RecordRequiresUserActionAsync(config, entry, ex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return await RecordFailureAsync(config, entry, startedAtLocal, ex, cancellationToken);
            }
            finally
            {
                TryDelete(packagePath);
            }
        }

        private async Task<ScheduledBackupRunOutcome> RunGoogleDriveExportAsync(
            ScheduledBackupConfig config,
            DateTime startedAtLocal,
            CancellationToken cancellationToken)
        {
            var startedAtUtc = _clock.UtcNow;
            var entry = CreateBaseLogEntry(config, startedAtUtc);
            string? packagePath = null;

            config.LastRunStartedAtUtc = startedAtUtc;
            config.LastRunCompletedAtUtc = null;
            config.LastErrorCode = null;
            config.LastErrorMessage = null;
            await _configStore.SaveAsync(config, cancellationToken);

            try
            {
                packagePath = await _packageCreator.CreatePackageAsync(config.ResourceKeys, cancellationToken);
                var stored = await _remoteStorage.StoreAsync(packagePath, config, cancellationToken);
                await _remoteStorage.PruneAsync(config, config.RetentionCount, cancellationToken);

                var completedAtUtc = _clock.UtcNow;
                entry.Status = ScheduledBackupRunStatus.Success;
                entry.CompletedAtUtc = completedAtUtc;
                entry.FileName = stored.FileName;
                entry.FilePath = stored.FilePath;
                entry.Bytes = stored.Bytes;

                config.RequiresUserAction = false;
                config.LastRunCompletedAtUtc = completedAtUtc;
                config.NextRunAtLocal = CardScheduleOccurrenceCalculator.GetNextOccurrence(
                    config.Schedule,
                    WallClockScheduleTime.NormalizeLocal(_clock.LocalNow));

                await _logStore.AppendAsync(entry, cancellationToken);
                await _logStore.PruneAsync(MaxLogEntries, cancellationToken);
                await _configStore.SaveAsync(config, cancellationToken);

                return new ScheduledBackupRunOutcome
                {
                    Result = ScheduledBackupRunResult.Success,
                    LogEntry = entry,
                    StoredFilePath = stored.FilePath
                };
            }
            catch (ScheduledBackupRequiresUserActionException ex)
            {
                return await RecordRequiresUserActionAsync(config, entry, ex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return await RecordFailureAsync(config, entry, startedAtLocal, ex, cancellationToken);
            }
            finally
            {
                TryDelete(packagePath);
            }
        }

        private async Task<ScheduledBackupRunOutcome> RecordRequiresUserActionAsync(
            ScheduledBackupConfig config,
            ScheduledBackupLogEntry entry,
            ScheduledBackupRequiresUserActionException exception,
            CancellationToken cancellationToken)
        {
            var completedAtUtc = _clock.UtcNow;
            entry.Status = ScheduledBackupRunStatus.RequiresUserAction;
            entry.CompletedAtUtc = completedAtUtc;
            entry.ErrorCode = exception.ErrorCode;
            entry.ErrorMessage = exception.Message;

            config.LastRunCompletedAtUtc = completedAtUtc;
            config.LastErrorCode = entry.ErrorCode;
            config.LastErrorMessage = entry.ErrorMessage;
            config.NextRunAtLocal = null;
            config.RequiresUserAction = true;

            await _logStore.AppendAsync(entry, cancellationToken);
            await _logStore.PruneAsync(MaxLogEntries, cancellationToken);
            await _configStore.SaveAsync(config, cancellationToken);

            return new ScheduledBackupRunOutcome
            {
                Result = ScheduledBackupRunResult.RequiresUserAction,
                LogEntry = entry
            };
        }

        private async Task<ScheduledBackupRunOutcome> RecordFailureAsync(
            ScheduledBackupConfig config,
            ScheduledBackupLogEntry entry,
            DateTime startedAtLocal,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var completedAtUtc = _clock.UtcNow;
            entry.Status = ScheduledBackupRunStatus.Failed;
            entry.CompletedAtUtc = completedAtUtc;
            entry.ErrorCode = exception.GetType().Name;
            entry.ErrorMessage = exception.Message;

            config.LastRunCompletedAtUtc = completedAtUtc;
            config.LastErrorCode = entry.ErrorCode;
            config.LastErrorMessage = entry.ErrorMessage;
            config.NextRunAtLocal = CardScheduleOccurrenceCalculator.GetNextOccurrence(
                config.Schedule,
                startedAtLocal);

            await _logStore.AppendAsync(entry, cancellationToken);
            await _logStore.PruneAsync(MaxLogEntries, cancellationToken);
            await _configStore.SaveAsync(config, cancellationToken);

            return new ScheduledBackupRunOutcome
            {
                Result = ScheduledBackupRunResult.Failed,
                LogEntry = entry
            };
        }

        private static ScheduledBackupLogEntry CreateBaseLogEntry(
            ScheduledBackupConfig config,
            DateTime startedAtUtc)
        {
            return new ScheduledBackupLogEntry
            {
                RunId = Guid.NewGuid(),
                StartedAtUtc = startedAtUtc,
                Status = ScheduledBackupRunStatus.Skipped,
                DestinationType = config.Destination.Type,
                DestinationDisplayName = string.IsNullOrWhiteSpace(config.Destination.DisplayName)
                    ? config.Destination.Type.ToString()
                    : config.Destination.DisplayName,
                ResourceKeys = config.ResourceKeys.ToList()
            };
        }

        private static void TryDelete(string? path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort cleanup for temporary export packages.
            }
        }
    }
}
