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
                PickerTitle = "Select Points backup zip or database file"
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

                throw new InvalidDataException("Select a .zip Points backup or a .db3 SQLite database file.");
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        public async Task<BackupImportPlan?> PickImportFolderAsync()
        {
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
