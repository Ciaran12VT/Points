using Points.Services.Sqlite.Interfaces;
using System.ComponentModel;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class DatabaseSettingsViewModel : Models.ObservableObject, INotifyPropertyChanged
    {
        private readonly IDbService _db;

        public DatabaseSettingsViewModel(IDbService db)
        {
            _db = db;

            BackupCommand = new Command(async () =>
            {
                await _db.BackupAsync();
                RefreshLastBackedUp();
            });

            RestoreCommand = new Command(async () =>
            {
                var pick = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a SQLite backup file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        [DevicePlatform.Android] = new[] { ".db", ".sqlite", ".sqlite3", ".bak" },
                    })
                });

                if (pick == null)
                    return;

                await _db.RestoreAsync(pick.FullPath);
                RefreshLastBackedUp();
            });

            BrowseBackupsCommand = new Command(async () =>
            {
                await Shell.Current.DisplayAlert("Backup folder", _db.BackupsFolderPath, "OK");
            });

            RefreshLastBackedUp();
        }

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand BrowseBackupsCommand { get; }

        private string _lastBackedUpText = "Never";
        public string LastBackedUpText
        {
            get => _lastBackedUpText;
            private set => SetProperty(ref _lastBackedUpText, value);
        }

        private void RefreshLastBackedUp()
        {
            var dt = _db.GetLastBackupUtc();
            LastBackedUpText = dt == null
                ? "Never"
                : $"{dt.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        public async Task WipeDatabase()
        {
            await _db.WipeAsync();
        }

        public async Task ExportDatabaseAsync()
        {
#if ANDROID
            var dbFolder = Path.Combine(FileSystem.AppDataDirectory, "db");
            var dbPath = Path.Combine(dbFolder, "points.db3");
            var backupPath = Path.Combine(FileSystem.CacheDirectory, $"points_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            File.Copy(dbPath, backupPath, overwrite: true);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export DB",
                File = new ShareFile(backupPath)
            });
#endif
        }

        public async Task ImportDatabaseAsync()
        {
#if ANDROID
            try
            {
                var dbFolder = Path.Combine(FileSystem.AppDataDirectory, "db");
                var dbPath = Path.Combine(dbFolder, "points.db3");

                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select database file to import"
                });

                if (result == null)
                    return;

                var destinationPath = dbPath;

                await _db.CloseDatabaseAsync();

                using var sourceStream = await result.OpenReadAsync();
                using var destinationStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write);

                await sourceStream.CopyToAsync(destinationStream);

                await _db.ReinitializeDatabaseAsync();

                RefreshLastBackedUp();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImportDatabaseAsync failed: {ex}");
                throw;
            }
#endif
        }
    }
}