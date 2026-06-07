namespace Points.Services.Backup
{
    public interface IScheduledBackupRemoteStorage
    {
        Task<ScheduledBackupStoredFile> StoreAsync(
            string packagePath,
            ScheduledBackupConfig config,
            CancellationToken cancellationToken = default);

        Task PruneAsync(
            ScheduledBackupConfig config,
            int retentionCount,
            CancellationToken cancellationToken = default);
    }

    public sealed class ScheduledBackupRequiresUserActionException : Exception
    {
        public ScheduledBackupRequiresUserActionException(string errorCode, string message)
            : base(message)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode)
                ? "RequiresUserAction"
                : errorCode;
        }

        public string ErrorCode { get; }
    }
}
