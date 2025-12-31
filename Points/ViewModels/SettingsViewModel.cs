using Points.Models;
using Points.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly IDbService _db;

        public SettingsViewModel(IDbService db)
        {
            _db = db;

            WipeCommand = new Command(async () => await _db.WipeAsync());
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
                        // Extensions only (works fine for Android)
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
                // Android generally can't "open a folder" directly in a reliable way.
                // So we surface the folder path (and/or you can navigate to a "Backups list" page later).
                await Shell.Current.DisplayAlert("Backup folder", _db.BackupsFolderPath, "OK");
            });

            RefreshLastBackedUp();
        }

        public ICommand WipeCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand BrowseBackupsCommand { get; }

        public string DatabaseSectionChevron => "⌄"; // purely cosmetic

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
    }
}
