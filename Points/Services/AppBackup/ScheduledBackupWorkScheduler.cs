namespace Points.Services.Backup
{
    public interface IScheduledBackupWorkScheduler
    {
        Task SyncAsync(CancellationToken cancellationToken = default);
        Task CancelAsync(CancellationToken cancellationToken = default);
    }

    public sealed class NullScheduledBackupWorkScheduler : IScheduledBackupWorkScheduler
    {
        public Task SyncAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CancelAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
