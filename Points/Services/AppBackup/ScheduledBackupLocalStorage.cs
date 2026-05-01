using Points.Global;
using Points.Services.Time;

namespace Points.Services.Backup
{
    public sealed class ScheduledBackupStoredFile
    {
        public string FileName { get; init; } = "";
        public string FilePath { get; init; } = "";
        public long Bytes { get; init; }
    }

    public interface IScheduledBackupLocalStorage
    {
        Task<ScheduledBackupStoredFile> StoreAsync(
            string packagePath,
            ScheduledBackupConfig config,
            CancellationToken cancellationToken = default);

        Task PruneAsync(int retentionCount, CancellationToken cancellationToken = default);
    }

    public sealed class AppPrivateScheduledBackupLocalStorage : IScheduledBackupLocalStorage
    {
        private const string FileNamePrefix = "points_scheduled_backup_";
        private const string FileExtension = ".zip";

        private readonly string _folderPath;
        private readonly IClock _clock;

        public AppPrivateScheduledBackupLocalStorage(IClock clock)
            : this(AppPaths.ScheduledBackupExportsFolder, clock)
        {
        }

        public AppPrivateScheduledBackupLocalStorage(string folderPath, IClock clock)
        {
            _folderPath = string.IsNullOrWhiteSpace(folderPath)
                ? throw new ArgumentException("Folder path is required.", nameof(folderPath))
                : folderPath;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<ScheduledBackupStoredFile> StoreAsync(
            string packagePath,
            ScheduledBackupConfig config,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("Package path is required.", nameof(packagePath));

            if (!File.Exists(packagePath))
                throw new FileNotFoundException("The scheduled backup package could not be found.", packagePath);

            Directory.CreateDirectory(_folderPath);

            var fileName = $"{FileNamePrefix}{_clock.LocalNow:yyyyMMdd_HHmmss}{FileExtension}";
            var destinationPath = GetAvailablePath(fileName);

            await using (var source = File.OpenRead(packagePath))
            await using (var destination = File.Create(destinationPath))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            File.SetLastWriteTimeUtc(destinationPath, _clock.UtcNow);

            return new ScheduledBackupStoredFile
            {
                FileName = Path.GetFileName(destinationPath),
                FilePath = destinationPath,
                Bytes = new FileInfo(destinationPath).Length
            };
        }

        public Task PruneAsync(int retentionCount, CancellationToken cancellationToken = default)
        {
            if (retentionCount < 1)
                retentionCount = 1;

            if (!Directory.Exists(_folderPath))
                return Task.CompletedTask;

            var filesToDelete = new DirectoryInfo(_folderPath)
                .EnumerateFiles($"{FileNamePrefix}*{FileExtension}", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.Ordinal)
                .Skip(retentionCount)
                .ToList();

            foreach (var file in filesToDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDelete(file.FullName);
            }

            return Task.CompletedTask;
        }

        private string GetAvailablePath(string fileName)
        {
            var destinationPath = Path.Combine(_folderPath, fileName);
            if (!File.Exists(destinationPath))
                return destinationPath;

            var stem = Path.GetFileNameWithoutExtension(fileName);
            for (var i = 1; i < 1000; i++)
            {
                destinationPath = Path.Combine(_folderPath, $"{stem}_{i:000}{FileExtension}");
                if (!File.Exists(destinationPath))
                    return destinationPath;
            }

            return Path.Combine(_folderPath, $"{stem}_{Guid.NewGuid():N}{FileExtension}");
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
                // Retention cleanup is best effort.
            }
        }
    }
}
