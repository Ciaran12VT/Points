using CommunityToolkit.Mvvm.ComponentModel;
using Points.Models;
using Points.Services.Sqlite.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class SettingsViewModel : Models.ObservableObject, INotifyPropertyChanged
    {
        private readonly IDbService _db;

        public SettingsViewModel(IDbService db)
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

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand BrowseBackupsCommand { get; }

        public ICommand SaveCommand { get; }

        public string DatabaseSectionChevron => "⌄"; // purely cosmetic
        public string MultiplierSectionChevron => "⌄";

        private string _lastBackedUpText = "Never";
        public string LastBackedUpText
        {
            get => _lastBackedUpText;
            private set => SetProperty(ref _lastBackedUpText, value);
        }

        private bool hardModeEnabled;

        // Bind the Entry to a string, then parse/validate.
        private string hardModeIdlePenaltyText = "-0.2";

        public double HardModeIdlePenaltyPerMinute
        {
            get
            {
                if (!double.TryParse(hardModeIdlePenaltyText, out var v))
                    return -0.0;

                // Force negative (idle penalty)
                return -Math.Abs(v);
            }
        }

        public bool IsHardModePenaltyValid
            => double.TryParse(hardModeIdlePenaltyText, out var v) && Math.Abs(v) > 0.0000001;

        // call this when saving settings
        public MultipliersSettings ToMultipliersSettings() => new()
        {
            HardModeEnabled = hardModeEnabled,
            HardModeIdlePenaltyPerMinute = HardModeIdlePenaltyPerMinute
        };

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
        // Let user pick a DB file (from Downloads, Drive, etc.)
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select database file to import"
        });

        if (result == null) return;

        var destinationPath = dbPath;

        // VERY IMPORTANT: ensure DB is not open
        await _db.CloseDatabaseAsync();

        // Copy selected DB over existing one
        using var sourceStream = await result.OpenReadAsync();
        using var destinationStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write);

        await sourceStream.CopyToAsync(destinationStream);

        System.Diagnostics.Debug.WriteLine("Database imported successfully.");

        // Optional but recommended: restart app or reinitialize DB services
        await _db.ReinitializeDatabaseAsync();
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"ImportDatabaseAsync failed: {ex}");
        throw;
    }
#endif
        }

    }

    public sealed class MultipliersSettings
    {
        public bool HardModeEnabled { get; set; }
        public double HardModeIdlePenaltyPerMinute { get; set; } = -0.2; // default negative
    }

}
