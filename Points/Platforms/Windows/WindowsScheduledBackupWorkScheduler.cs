#if WINDOWS
using Points.Services.Backup;
using Points.Services.Scheduling;
using Points.Services.Time;

namespace Points.Platforms.Windows
{
    public sealed class WindowsScheduledBackupWorkScheduler : IScheduledBackupWorkScheduler, IDisposable
    {
        private static readonly TimeSpan MinimumDelay = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MaximumDelay = TimeSpan.FromDays(1);

        private readonly IScheduledBackupConfigStore _configStore;
        private readonly IScheduledBackupRunner _runner;
        private readonly IClock _clock;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private CancellationTokenSource? _loopCancellation;
        private Task? _loopTask;
        private bool _disposed;

        public WindowsScheduledBackupWorkScheduler(
            IScheduledBackupConfigStore configStore,
            IScheduledBackupRunner runner,
            IClock clock)
        {
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!BackupFeatureFlags.AutomaticExportRuntimeEnabled)
            {
                await CancelAsync(cancellationToken);
                return;
            }

            var config = await PrepareConfigAsync(cancellationToken);
            if (!ShouldSchedule(config))
            {
                await CancelAsync(cancellationToken);
                return;
            }

            var delay = GetDelay(config);
            await _gate.WaitAsync(cancellationToken);
            try
            {
                StopLoop();

                _loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _loopTask = Task.Run(
                    () => RunLoopAsync(delay, _loopCancellation.Token),
                    CancellationToken.None);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task CancelAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            await _gate.WaitAsync(cancellationToken);
            try
            {
                StopLoop();
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task RunLoopAsync(TimeSpan initialDelay, CancellationToken cancellationToken)
        {
            var delay = initialDelay;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken);
                    await _runner.RunDueAsync(cancellationToken);

                    var config = await PrepareConfigAsync(cancellationToken);
                    if (!ShouldSchedule(config))
                        break;

                    delay = GetDelay(config);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Windows scheduled backup loop failed: {ex}");
                    delay = TimeSpan.FromMinutes(15);
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

        private TimeSpan GetDelay(ScheduledBackupConfig config)
        {
            var nextRun = WallClockScheduleTime.NormalizeLocal(config.NextRunAtLocal);
            if (!nextRun.HasValue)
                return TimeSpan.FromMinutes(15);

            var delay = nextRun.Value - WallClockScheduleTime.NormalizeLocal(_clock.LocalNow);
            if (delay < MinimumDelay)
                return MinimumDelay;

            return delay > MaximumDelay ? MaximumDelay : delay;
        }

        private void StopLoop()
        {
            _loopCancellation?.Cancel();
            _loopCancellation?.Dispose();
            _loopCancellation = null;
            _loopTask = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowsScheduledBackupWorkScheduler));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopLoop();
            _gate.Dispose();
            _disposed = true;
        }
    }
}
#endif
