using CommunityToolkit.Maui.Storage;

namespace Points.Services.Backup
{
    public enum BackupStorageLocation
    {
        DeviceStorage,
        GoogleDrive
    }

    public sealed class BackupStorageLocationOption
    {
        public BackupStorageLocation Location { get; init; }
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
    }

    public sealed class BackupPickedFile
    {
        public string DisplayName { get; init; } = "";
        public string TempPath { get; init; } = "";
    }

    public sealed class BackupExportResult
    {
        public BackupStorageLocation Location { get; init; }
        public string DisplayLocation { get; init; } = "";
        public string? FilePath { get; init; }
    }

    public interface IBackupFileStorageService
    {
        IReadOnlyList<BackupStorageLocationOption> GetExportLocations();
        IReadOnlyList<BackupStorageLocationOption> GetFileImportLocations();
        Task<BackupExportResult?> SaveExportPackageAsync(
            string packagePath,
            BackupStorageLocation location,
            CancellationToken cancellationToken = default);
        Task<BackupPickedFile?> PickImportFileAsync(
            BackupStorageLocation location,
            CancellationToken cancellationToken = default);
    }

    public sealed class BackupFileStorageService : IBackupFileStorageService
    {
        private static readonly IReadOnlyList<BackupStorageLocationOption> ExportLocations =
        [
            new()
            {
                Location = BackupStorageLocation.DeviceStorage,
                Title = "Device storage",
                Description = "Save the backup using the device file picker."
            },
            new()
            {
                Location = BackupStorageLocation.GoogleDrive,
                Title = "Google Drive",
                Description = "Save the backup to Google Drive when Drive is available in the file picker."
            }
        ];

        private static readonly IReadOnlyList<BackupStorageLocationOption> FileImportLocations =
        [
            new()
            {
                Location = BackupStorageLocation.DeviceStorage,
                Title = "Device storage",
                Description = "Import a backup file from the device."
            },
            new()
            {
                Location = BackupStorageLocation.GoogleDrive,
                Title = "Google Drive",
                Description = "Import a backup file from Google Drive when Drive is available in the file picker."
            }
        ];

        public IReadOnlyList<BackupStorageLocationOption> GetExportLocations()
        {
            return BackupFeatureFlags.GoogleDriveStorageUiEnabled
                ? ExportLocations
                : ExportLocations
                    .Where(location => location.Location != BackupStorageLocation.GoogleDrive)
                    .ToList();
        }

        public IReadOnlyList<BackupStorageLocationOption> GetFileImportLocations()
        {
            return BackupFeatureFlags.GoogleDriveStorageUiEnabled
                ? FileImportLocations
                : FileImportLocations
                    .Where(location => location.Location != BackupStorageLocation.GoogleDrive)
                    .ToList();
        }

        public async Task<BackupExportResult?> SaveExportPackageAsync(
            string packagePath,
            BackupStorageLocation location,
            CancellationToken cancellationToken = default)
        {
            await using var packageStream = File.OpenRead(packagePath);
            var result = await FileSaver.Default.SaveAsync(
                Path.GetFileName(packagePath),
                packageStream,
                cancellationToken);

            if (!result.IsSuccessful)
            {
                if (result.Exception != null)
                    throw result.Exception;

                return null;
            }

            return new BackupExportResult
            {
                Location = location,
                DisplayLocation = GetDisplayLocation(location),
                FilePath = result.FilePath
            };
        }

        public async Task<BackupPickedFile?> PickImportFileAsync(
            BackupStorageLocation location,
            CancellationToken cancellationToken = default)
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = location == BackupStorageLocation.GoogleDrive
                    ? "Select a Points backup from Google Drive"
                    : "Select a Points .zip backup or .db3 database"
            });

            if (result == null)
                return null;

            cancellationToken.ThrowIfCancellationRequested();

            var displayName = !string.IsNullOrWhiteSpace(result.FileName)
                ? result.FileName
                : result.FullPath ?? "";
            var extension = (Path.GetExtension(displayName) ?? "").ToLowerInvariant();
            var tempPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"points_import_{Guid.NewGuid():N}{extension}");

            await using (var sourceStream = await result.OpenReadAsync())
            await using (var destinationStream = File.Create(tempPath))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            return new BackupPickedFile
            {
                DisplayName = displayName,
                TempPath = tempPath
            };
        }

        private static string GetDisplayLocation(BackupStorageLocation location)
        {
            return location switch
            {
                BackupStorageLocation.GoogleDrive => "Google Drive",
                _ => "Device storage"
            };
        }
    }
}
