#if IOS
#pragma warning disable CA1416 // BGTaskScheduler calls are guarded by iOS 13 runtime checks.
using BackgroundTasks;
using Foundation;
using Points.Services.Backup;
using Points.Services.Scheduling;
using Points.Services.Time;

namespace Points.Platforms.iOS
{
    public sealed class IosScheduledBackupWorkScheduler : IScheduledBackupWorkScheduler
    {
        private const string TaskIdentifier = "com.companyname.points.scheduled-backup";
        private const double MinimumDelaySeconds = 60;

        private static readonly object RegistrationGate = new();
        private static bool _registrationAttempted;

        private readonly IScheduledBackupConfigStore _configStore;
        private readonly IScheduledBackupRunner _runner;
        private readonly IClock _clock;

        public IosScheduledBackupWorkScheduler(
            IScheduledBackupConfigStore configStore,
            IScheduledBackupRunner runner,
            IClock clock)
        {
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            RegisterBackgroundTask();
        }

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            if (!BackupFeatureFlags.AutomaticExportRuntimeEnabled)
            {
                await CancelAsync(cancellationToken);
                return;
            }

            if (!OperatingSystem.IsIOSVersionAtLeast(13))
                return;

            var config = await PrepareConfigAsync(cancellationToken);
            if (!ShouldSchedule(config))
            {
                await CancelAsync(cancellationToken);
                return;
            }

            var request = new BGProcessingTaskRequest(TaskIdentifier)
            {
                EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(GetDelaySeconds(config)),
                RequiresExternalPower = false,
                RequiresNetworkConnectivity = config.Destination.Type == ScheduledBackupDestinationType.GoogleDrive
            };

            if (!BGTaskScheduler.Shared.Submit(request, out var error) && error != null)
                System.Diagnostics.Debug.WriteLine($"iOS scheduled backup submit failed: {error.LocalizedDescription}");
        }

        public Task CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OperatingSystem.IsIOSVersionAtLeast(13))
                BGTaskScheduler.Shared.Cancel(TaskIdentifier);

            return Task.CompletedTask;
        }

        private void RegisterBackgroundTask()
        {
            if (!OperatingSystem.IsIOSVersionAtLeast(13))
                return;

            lock (RegistrationGate)
            {
                if (_registrationAttempted)
                    return;

                _registrationAttempted = true;

                var registered = BGTaskScheduler.Shared.Register(
                    TaskIdentifier,
                    null,
                    task => _ = HandleTaskAsync(task));

                if (!registered)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"iOS scheduled backup task '{TaskIdentifier}' was not registered. Check BGTaskSchedulerPermittedIdentifiers in Info.plist.");
                }
            }
        }

        private async Task HandleTaskAsync(BGTask task)
        {
            using var cancellation = new CancellationTokenSource();
            task.ExpirationHandler = cancellation.Cancel;

            var success = false;

            try
            {
                var outcome = await _runner.RunDueAsync(cancellation.Token);
                success = outcome.Result != ScheduledBackupRunResult.Failed;
            }
            catch (OperationCanceledException)
            {
                success = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iOS scheduled backup task failed: {ex}");
                success = false;
            }
            finally
            {
                task.SetTaskCompleted(success);

                try
                {
                    await SyncAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"iOS scheduled backup reschedule failed: {ex}");
                }
            }
        }

        private async Task<ScheduledBackupConfig> PrepareConfigAsync(CancellationToken cancellationToken)
        {
            var config = await _configStore.GetAsync(cancellationToken);

            if (ShouldSchedule(config) && !config.NextRunAtLocal.HasValue)
            {
                config.NextRunAtLocal = CardScheduleOccurrenceCalculator.GetNextOccurrence(
                    config.Schedule,
                    _clock.LocalNow);
                await _configStore.SaveAsync(config, cancellationToken);
            }

            return config;
        }

        private static bool ShouldSchedule(ScheduledBackupConfig config)
        {
            return config.IsEnabled &&
                   config.Schedule?.IsEnabled == true &&
                   !config.RequiresUserAction;
        }

        private double GetDelaySeconds(ScheduledBackupConfig config)
        {
            var nextRun = WallClockScheduleTime.NormalizeLocal(config.NextRunAtLocal);
            if (!nextRun.HasValue)
                return TimeSpan.FromMinutes(15).TotalSeconds;

            var delay = nextRun.Value - WallClockScheduleTime.NormalizeLocal(_clock.LocalNow);
            return Math.Max(MinimumDelaySeconds, delay.TotalSeconds);
        }
    }
}
#pragma warning restore CA1416
#endif
