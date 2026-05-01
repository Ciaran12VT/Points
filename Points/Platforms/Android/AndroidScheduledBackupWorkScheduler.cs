#if ANDROID
using Android.Content;
using AndroidX.Work;
using Points.Services.Backup;
using AndroidApp = Android.App;

namespace Points.Platforms.Android
{
    public sealed class AndroidScheduledBackupWorkScheduler : IScheduledBackupWorkScheduler
    {
        private const string UniqueWorkName = "points_scheduled_backup";
        private static readonly TimeSpan RepeatInterval = TimeSpan.FromMinutes(15);

        private readonly Context _context;
        private readonly IScheduledBackupConfigStore _configStore;

        public AndroidScheduledBackupWorkScheduler(IScheduledBackupConfigStore configStore)
        {
            _context = AndroidApp.Application.Context;
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        }

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            var config = await _configStore.GetAsync(cancellationToken);

            if (!config.IsEnabled || config.Schedule?.IsEnabled != true || config.RequiresUserAction)
            {
                await CancelAsync(cancellationToken);
                return;
            }

            var requiredNetworkType = config.Destination.Type == ScheduledBackupDestinationType.GoogleDrive
                ? NetworkType.Connected!
                : NetworkType.NotRequired!;

            var constraints = new Constraints.Builder()
                .SetRequiredNetworkType(requiredNetworkType)
                .Build();

            var request = PeriodicWorkRequest.Builder
                .From<ScheduledBackupWorker>(RepeatInterval)
                .SetConstraints(constraints)
                .AddTag(UniqueWorkName)
                .Build();

            WorkManager
                .GetInstance(_context)
                .EnqueueUniquePeriodicWork(
                    UniqueWorkName,
                    ExistingPeriodicWorkPolicy.CancelAndReenqueue!,
                    (PeriodicWorkRequest)request);
        }

        public Task CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WorkManager
                .GetInstance(_context)
                .CancelUniqueWork(UniqueWorkName);

            return Task.CompletedTask;
        }
    }
}
#endif
