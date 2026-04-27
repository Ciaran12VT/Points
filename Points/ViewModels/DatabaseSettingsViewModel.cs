using CommunityToolkit.Maui.Storage;
using Points.Services.Backup;
using Points.Services.Sqlite.Interfaces;

namespace Points.ViewModels
{
    public class DatabaseSettingsViewModel
    {
        private readonly IDbService _db;

        public DatabaseSettingsViewModel(IDbService db)
        {
            _db = db;
        }

        public async Task WipeDatabase()
        {
            await _db.WipeAsync();
        }

        public IReadOnlyList<BackupResourceOption> GetExportableItems()
        {
            return BackupPackageService.GetExportableResources();
        }

        public async Task<string?> ExportDatabaseAsync(IEnumerable<string> selectedKeys)
        {
            var packagePath = await BackupPackageService.CreateExportPackageAsync(_db, selectedKeys);

            try
            {
                await using var packageStream = File.OpenRead(packagePath);
                var result = await FileSaver.Default.SaveAsync(
                    Path.GetFileName(packagePath),
                    packageStream,
                    CancellationToken.None);

                if (!result.IsSuccessful)
                {
                    if (result.Exception != null)
                        throw result.Exception;

                    return null;
                }

                return result.FilePath;
            }
            finally
            {
                TryDelete(packagePath);
            }
        }

        public async Task<BackupImportPlan?> PickImportFileAsync()
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a Points .zip backup or .db3 database"
            });

            if (result == null)
                return null;

            var fileName = !string.IsNullOrWhiteSpace(result.FileName)
                ? result.FileName
                : result.FullPath ?? "";
            var extension = (Path.GetExtension(fileName) ?? "").ToLowerInvariant();
            var tempPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"points_import_{Guid.NewGuid():N}{extension}");

            await using (var sourceStream = await result.OpenReadAsync())
            await using (var destinationStream = File.Create(tempPath))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }

            try
            {
                if (extension == ".zip")
                    return await BackupPackageService.InspectZipPackageAsync(tempPath);

                if (extension is ".db" or ".db3" or ".sqlite" or ".sqlite3")
                    return BackupPackageService.CreateLegacyDatabaseImportPlan(tempPath, new[] { tempPath });

                throw new InvalidDataException("Select a Points .zip backup package, or a legacy .db3 SQLite database file.");
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        public async Task<BackupImportPlan?> PickImportFolderAsync()
        {
#if ANDROID
            await Task.CompletedTask;
            throw new PlatformNotSupportedException(
                "Folder import is not supported on Android because Android does not grant Points direct access to every file inside a selected folder. Import the exported .zip backup package instead.");
#else
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);

            if (!result.IsSuccessful)
            {
                if (result.Exception != null)
                    throw result.Exception;

                return null;
            }

            if (result.Folder == null || string.IsNullOrWhiteSpace(result.Folder.Path))
                return null;

            return BackupPackageService.InspectPackageFolder(result.Folder.Path);
#endif
        }

        public async Task ImportDatabaseAsync(BackupImportPlan plan, IEnumerable<string> selectedKeys)
        {
            try
            {
                await BackupPackageService.RestoreAsync(_db, plan, selectedKeys);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import failed: {ex}");
                throw;
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
                // Best effort cleanup for cache files.
            }
        }
    }
}
