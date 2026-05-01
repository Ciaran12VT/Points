using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Backup
{
    public sealed class BackupPackageScheduledBackupPackageCreator : IScheduledBackupPackageCreator
    {
        private readonly IDatabaseInitializationService _databaseLifecycle;
        private readonly IClock _clock;

        public BackupPackageScheduledBackupPackageCreator(
            IDatabaseInitializationService databaseLifecycle,
            IClock clock)
        {
            _databaseLifecycle = databaseLifecycle ?? throw new ArgumentNullException(nameof(databaseLifecycle));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Task<string> CreatePackageAsync(
            IEnumerable<string> resourceKeys,
            CancellationToken cancellationToken = default)
        {
            return BackupPackageService.CreateExportPackageAsync(
                _databaseLifecycle,
                resourceKeys,
                cancellationToken,
                _clock);
        }
    }
}
