using CommunityToolkit.Maui.Storage;
using Points.Models;
using Points.Services.Backup;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Scheduling;
using Points.Services.Time;

namespace Points.ViewModels.Settings
{
    public class DatabaseSettingsViewModel : ObservableObject
    {
        private readonly IDatabaseMaintenanceService _databaseMaintenance;
        private readonly IDatabaseInitializationService _databaseLifecycle;
        private readonly IClock _clock;
        private readonly IAppDialogService _dialogs;
        private readonly IBackupFileStorageService _backupFileStorage;
        private readonly IScheduledBackupConfigStore _scheduledBackupConfigStore;
        private readonly IScheduledBackupLogStore _scheduledBackupLogStore;
        private readonly IGoogleDriveBackupConnector _googleDriveBackupConnector;
        private readonly IScheduledBackupWorkScheduler _scheduledBackupWorkScheduler;
        private ScheduledBackupConfig _automaticExportConfig = ScheduledBackupConfig.DisabledDefault();
        private string _automaticExportStatus = "Off";
        private string _automaticExportDetail = "No automatic export configured.";
        private string _automaticExportResourcesText = "Database";
        private string _automaticExportScheduleText = "Every day at 02:00";
        private string _automaticExportDestinationText = "App exports folder";
        private string _automaticExportRetentionText = "Keep last 7 backups";
        private string _automaticExportLastRunText = "No runs yet";
        private string _automaticExportErrorText = "";
        private string _automaticExportToggleText = "Enable";
        private bool _automaticExportHasError;
        private bool _automaticExportCanReconnect;

        public Command WipeDatabaseCommand { get; }

        public DatabaseSettingsViewModel(
            IDatabaseMaintenanceService databaseMaintenance,
            IDatabaseInitializationService databaseLifecycle,
            IClock clock,
            IAppDialogService dialogs,
            IBackupFileStorageService backupFileStorage,
            IScheduledBackupConfigStore scheduledBackupConfigStore,
            IScheduledBackupLogStore scheduledBackupLogStore,
            IGoogleDriveBackupConnector googleDriveBackupConnector,
            IScheduledBackupWorkScheduler scheduledBackupWorkScheduler)
        {
            _databaseMaintenance = databaseMaintenance;
            _databaseLifecycle = databaseLifecycle;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _backupFileStorage = backupFileStorage ?? throw new ArgumentNullException(nameof(backupFileStorage));
            _scheduledBackupConfigStore = scheduledBackupConfigStore ?? throw new ArgumentNullException(nameof(scheduledBackupConfigStore));
            _scheduledBackupLogStore = scheduledBackupLogStore ?? throw new ArgumentNullException(nameof(scheduledBackupLogStore));
            _googleDriveBackupConnector = googleDriveBackupConnector ?? throw new ArgumentNullException(nameof(googleDriveBackupConnector));
            _scheduledBackupWorkScheduler = scheduledBackupWorkScheduler ?? throw new ArgumentNullException(nameof(scheduledBackupWorkScheduler));

            WipeDatabaseCommand = new Command(async () => await ConfirmAndWipeDatabaseAsync());
            RefreshAutomaticExportSummary(null);
        }

        public string AutomaticExportStatus
        {
            get => _automaticExportStatus;
            private set => SetProperty(ref _automaticExportStatus, value);
        }

        public string AutomaticExportDetail
        {
            get => _automaticExportDetail;
            private set => SetProperty(ref _automaticExportDetail, value);
        }

        public string AutomaticExportResourcesText
        {
            get => _automaticExportResourcesText;
            private set => SetProperty(ref _automaticExportResourcesText, value);
        }

        public string AutomaticExportScheduleText
        {
            get => _automaticExportScheduleText;
            private set => SetProperty(ref _automaticExportScheduleText, value);
        }

        public string AutomaticExportDestinationText
        {
            get => _automaticExportDestinationText;
            private set => SetProperty(ref _automaticExportDestinationText, value);
        }

        public string AutomaticExportRetentionText
        {
            get => _automaticExportRetentionText;
            private set => SetProperty(ref _automaticExportRetentionText, value);
        }

        public string AutomaticExportLastRunText
        {
            get => _automaticExportLastRunText;
            private set => SetProperty(ref _automaticExportLastRunText, value);
        }

        public string AutomaticExportErrorText
        {
            get => _automaticExportErrorText;
            private set => SetProperty(ref _automaticExportErrorText, value);
        }

        public string AutomaticExportToggleText
        {
            get => _automaticExportToggleText;
            private set => SetProperty(ref _automaticExportToggleText, value);
        }

        public bool AutomaticExportHasError
        {
            get => _automaticExportHasError;
            private set => SetProperty(ref _automaticExportHasError, value);
        }

        public bool AutomaticExportCanReconnect
        {
            get => _automaticExportCanReconnect;
            private set => SetProperty(ref _automaticExportCanReconnect, value);
        }

        public async Task WipeDatabase()
        {
            await _databaseMaintenance.WipeAsync();
        }

        public async Task ConfirmAndWipeDatabaseAsync()
        {
            var input = await _dialogs.DisplayPromptAsync(
                "Wipe DB",
                "Are you sure you want to wipe the DB? To proceed, type exactly \"Wipe db\".",
                "Wipe",
                "Cancel");

            if (input == "Wipe db")
                await WipeDatabase();
        }

        public IReadOnlyList<BackupResourceOption> GetExportableItems()
        {
            return BackupPackageService.GetExportableResources();
        }

        public async Task LoadAutomaticExportConfigAsync()
        {
            _automaticExportConfig = await _scheduledBackupConfigStore.GetAsync();
            var lastRun = (await _scheduledBackupLogStore.GetRecentAsync(1)).FirstOrDefault();
            RefreshAutomaticExportSummary(lastRun);
        }

        public ScheduledBackupConfig GetAutomaticExportDraft()
        {
            return Clone(_automaticExportConfig);
        }

        public async Task SaveAutomaticExportConfigAsync(ScheduledBackupConfig config)
        {
            _automaticExportConfig = PrepareForSave(config);
            await _scheduledBackupConfigStore.SaveAsync(_automaticExportConfig);
            await _scheduledBackupWorkScheduler.SyncAsync();
            var lastRun = (await _scheduledBackupLogStore.GetRecentAsync(1)).FirstOrDefault();
            RefreshAutomaticExportSummary(lastRun);
        }

        public async Task ToggleAutomaticExportAsync()
        {
            var draft = GetAutomaticExportDraft();
            var enable = !draft.IsEnabled;

            if (enable &&
                draft.Destination.Type == ScheduledBackupDestinationType.GoogleDrive &&
                (draft.RequiresUserAction || string.IsNullOrWhiteSpace(draft.Destination.GoogleDriveCredentialKey)))
            {
                await _dialogs.DisplayAlertAsync(
                    "Google Drive",
                    "Reconnect Google Drive from Configure before enabling automatic export.",
                    "OK");
                return;
            }

            draft.IsEnabled = enable;
            draft.Schedule.IsEnabled = enable;

            await SaveAutomaticExportConfigAsync(draft);
        }

        public async Task<ScheduledBackupDestinationConfig> ConnectGoogleDriveDestinationAsync(
            Func<GoogleDriveDeviceAuthorizationInfo, Task> presentAuthorizationAsync)
        {
            var connection = await _googleDriveBackupConnector.ConnectAsync(presentAuthorizationAsync);

            return new ScheduledBackupDestinationConfig
            {
                Type = ScheduledBackupDestinationType.GoogleDrive,
                DisplayName = "Google Drive",
                GoogleDriveAccountEmail = connection.AccountEmail,
                GoogleDriveFolderId = connection.FolderId,
                GoogleDriveFolderName = connection.FolderName,
                GoogleDriveCredentialKey = connection.CredentialKey
            };
        }

        public async Task ReconnectAutomaticExportGoogleDriveAsync(
            Func<GoogleDriveDeviceAuthorizationInfo, Task> presentAuthorizationAsync)
        {
            if (_automaticExportConfig.Destination.Type != ScheduledBackupDestinationType.GoogleDrive)
            {
                await _dialogs.DisplayAlertAsync(
                    "Google Drive",
                    "Automatic export is not configured for Google Drive.",
                    "OK");
                return;
            }

            var draft = GetAutomaticExportDraft();
            draft.Destination = await ConnectGoogleDriveDestinationAsync(presentAuthorizationAsync);
            draft.RequiresUserAction = false;
            draft.LastErrorCode = null;
            draft.LastErrorMessage = null;

            await SaveAutomaticExportConfigAsync(draft);
        }

        public IReadOnlyList<BackupStorageLocationOption> GetExportLocations()
        {
            return _backupFileStorage.GetExportLocations();
        }

        public IReadOnlyList<BackupStorageLocationOption> GetFileImportLocations()
        {
            return _backupFileStorage.GetFileImportLocations();
        }

        public async Task<BackupExportResult?> ExportDatabaseAsync(
            IEnumerable<string> selectedKeys,
            BackupStorageLocation location)
        {
            var packagePath = await BackupPackageService.CreateExportPackageAsync(_databaseLifecycle, selectedKeys, clock: _clock);

            try
            {
                return await _backupFileStorage.SaveExportPackageAsync(packagePath, location);
            }
            finally
            {
                TryDelete(packagePath);
            }
        }

        public async Task<BackupImportPlan?> PickImportFileAsync(
            BackupStorageLocation location = BackupStorageLocation.DeviceStorage)
        {
            var pickedFile = await _backupFileStorage.PickImportFileAsync(location);
            if (pickedFile == null)
                return null;

            var extension = (Path.GetExtension(pickedFile.DisplayName) ?? "").ToLowerInvariant();

            try
            {
                if (extension == ".zip")
                    return await BackupPackageService.InspectZipPackageAsync(pickedFile.TempPath);

                if (extension is ".db" or ".db3" or ".sqlite" or ".sqlite3")
                    return BackupPackageService.CreateLegacyDatabaseImportPlan(pickedFile.TempPath, new[] { pickedFile.TempPath });

                throw new InvalidDataException("Select a Points .zip backup package, or a legacy .db3 SQLite database file.");
            }
            catch
            {
                TryDelete(pickedFile.TempPath);
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
                await BackupPackageService.RestoreAsync(_databaseLifecycle, plan, selectedKeys);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import failed: {ex}");
                throw;
            }
        }

        private ScheduledBackupConfig PrepareForSave(ScheduledBackupConfig config)
        {
            var prepared = Clone(config);
            prepared.Version = ScheduledBackupConfig.CurrentVersion;
            prepared.Schedule ??= ScheduledBackupSchedule.Default();
            prepared.Destination ??= ScheduledBackupDestinationConfig.DeviceStorage();
            prepared.ResourceKeys = prepared.ResourceKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (prepared.ResourceKeys.Count == 0)
                prepared.ResourceKeys.Add("database");

            if (prepared.RetentionCount < 1)
                prepared.RetentionCount = 1;

            prepared.Schedule.FromDateTime = WallClockScheduleTime.NormalizeLocal(prepared.Schedule.FromDateTime);
            prepared.Schedule.ToDateTime = WallClockScheduleTime.NormalizeLocal(prepared.Schedule.ToDateTime);
            prepared.IsEnabled = prepared.IsEnabled && prepared.Schedule.IsEnabled;

            if (prepared.Destination.Type == ScheduledBackupDestinationType.GoogleDrive &&
                string.IsNullOrWhiteSpace(prepared.Destination.GoogleDriveCredentialKey))
            {
                prepared.RequiresUserAction = true;
                prepared.LastErrorCode = "GoogleDriveReconnectRequired";
                prepared.LastErrorMessage = "Reconnect Google Drive to finish automatic export setup.";
            }
            else
            {
                prepared.RequiresUserAction = false;

                if (prepared.LastErrorCode?.StartsWith("GoogleDrive", StringComparison.Ordinal) == true)
                {
                    prepared.LastErrorCode = null;
                    prepared.LastErrorMessage = null;
                }
            }

            prepared.NextRunAtLocal = prepared.IsEnabled && !prepared.RequiresUserAction
                ? CardScheduleOccurrenceCalculator.GetNextOccurrence(prepared.Schedule, _clock.LocalNow)
                : null;

            return prepared;
        }

        private void RefreshAutomaticExportSummary(ScheduledBackupLogEntry? lastRun)
        {
            var config = _automaticExportConfig;

            AutomaticExportStatus = config.RequiresUserAction
                ? "Needs attention"
                : config.IsEnabled ? "Enabled" : "Off";

            AutomaticExportDetail = config.RequiresUserAction
                ? config.LastErrorMessage ?? "Action required."
                : config.IsEnabled
                    ? config.NextRunAtLocal.HasValue
                        ? $"Next export: {config.NextRunAtLocal.Value:yyyy-MM-dd HH:mm}"
                        : "No future export scheduled."
                    : "Automatic export is disabled.";

            AutomaticExportResourcesText = FormatResourceKeys(config.ResourceKeys);
            AutomaticExportScheduleText = FormatSchedule(config.Schedule);
            AutomaticExportDestinationText = FormatDestination(config.Destination);
            AutomaticExportRetentionText = FormatRetention(config.RetentionCount);
            AutomaticExportLastRunText = FormatLastRun(config, lastRun);
            AutomaticExportErrorText = config.LastErrorMessage ?? "";
            AutomaticExportHasError = config.RequiresUserAction || !string.IsNullOrWhiteSpace(config.LastErrorMessage);
            AutomaticExportCanReconnect = config.Destination.Type == ScheduledBackupDestinationType.GoogleDrive &&
                (config.RequiresUserAction ||
                 string.IsNullOrWhiteSpace(config.Destination.GoogleDriveCredentialKey) ||
                 IsGoogleDriveError(config.LastErrorCode));
            AutomaticExportToggleText = config.IsEnabled ? "Disable" : "Enable";
        }

        private string FormatResourceKeys(IReadOnlyCollection<string> keys)
        {
            var selected = keys.ToHashSet(StringComparer.Ordinal);
            var titles = GetExportableItems()
                .Where(resource => selected.Contains(resource.Key))
                .Select(resource => resource.Title)
                .ToList();

            return titles.Count == 0 ? "Database" : string.Join(", ", titles);
        }

        private static string FormatSchedule(ScheduledBackupSchedule schedule)
        {
            var time = schedule.FromDateTime.ToString("HH:mm");
            var enabled = schedule.IsEnabled ? "" : " (disabled)";

            var core = schedule.FrequencyType switch
            {
                FrequencyType.Once => $"Once at {time}",
                FrequencyType.EveryDays => $"Every {Math.Max(1, schedule.FrequencyValue)} day(s) at {time}",
                FrequencyType.EveryWeekday => $"Every weekday at {time}",
                FrequencyType.EveryMonday => $"Every Monday at {time}",
                FrequencyType.EveryTuesday => $"Every Tuesday at {time}",
                FrequencyType.EveryWednesday => $"Every Wednesday at {time}",
                FrequencyType.EveryThursday => $"Every Thursday at {time}",
                FrequencyType.EveryFriday => $"Every Friday at {time}",
                FrequencyType.EverySaturday => $"Every Saturday at {time}",
                FrequencyType.EverySunday => $"Every Sunday at {time}",
                FrequencyType.EveryWeeks => $"Every week at {time}",
                FrequencyType.EveryMonths => $"Every month at {time}",
                FrequencyType.EveryYears => $"Every year at {time}",
                _ => schedule.FrequencyType.ToString()
            };

            return $"{core}{enabled}";
        }

        private static string FormatDestination(ScheduledBackupDestinationConfig destination)
        {
            return destination.Type switch
            {
                ScheduledBackupDestinationType.GoogleDrive => string.IsNullOrWhiteSpace(destination.GoogleDriveAccountEmail)
                    ? "Google Drive"
                    : $"Google Drive ({destination.GoogleDriveAccountEmail})",
                _ => string.IsNullOrWhiteSpace(destination.DisplayName) ? "App exports folder" : destination.DisplayName
            };
        }

        private static string FormatRetention(int retentionCount)
        {
            var count = Math.Max(1, retentionCount);
            return count == 1 ? "Keep latest backup" : $"Keep latest {count} backups";
        }

        private static string FormatLastRun(ScheduledBackupConfig config, ScheduledBackupLogEntry? lastRun)
        {
            if (lastRun != null)
            {
                var when = lastRun.CompletedAtUtc ?? lastRun.StartedAtUtc;
                return $"{lastRun.Status} at {when:yyyy-MM-dd HH:mm} UTC";
            }

            if (config.LastRunCompletedAtUtc.HasValue)
                return $"Completed at {config.LastRunCompletedAtUtc.Value:yyyy-MM-dd HH:mm} UTC";

            return "No runs yet";
        }

        private static bool IsGoogleDriveError(string? errorCode)
        {
            return errorCode?.StartsWith("GoogleDrive", StringComparison.Ordinal) == true;
        }

        private static ScheduledBackupConfig Clone(ScheduledBackupConfig config)
        {
            return new ScheduledBackupConfig
            {
                Version = config.Version,
                IsEnabled = config.IsEnabled,
                Schedule = new ScheduledBackupSchedule
                {
                    FrequencyType = config.Schedule?.FrequencyType ?? FrequencyType.EveryDays,
                    FrequencyValue = config.Schedule?.FrequencyValue ?? 1,
                    FromDateTime = config.Schedule?.FromDateTime ?? ScheduledBackupSchedule.Default().FromDateTime,
                    ToDateTime = config.Schedule?.ToDateTime,
                    IsEnabled = config.Schedule?.IsEnabled ?? true,
                    Note = config.Schedule?.Note ?? ""
                },
                ResourceKeys = config.ResourceKeys?.ToList() ?? new List<string> { "database" },
                Destination = new ScheduledBackupDestinationConfig
                {
                    Type = config.Destination?.Type ?? ScheduledBackupDestinationType.DeviceStorage,
                    DisplayName = config.Destination?.DisplayName ?? "App exports folder",
                    DeviceFolderPath = config.Destination?.DeviceFolderPath,
                    DeviceFolderUri = config.Destination?.DeviceFolderUri,
                    GoogleDriveAccountEmail = config.Destination?.GoogleDriveAccountEmail,
                    GoogleDriveFolderId = config.Destination?.GoogleDriveFolderId,
                    GoogleDriveFolderName = config.Destination?.GoogleDriveFolderName,
                    GoogleDriveCredentialKey = config.Destination?.GoogleDriveCredentialKey
                },
                RetentionCount = config.RetentionCount,
                LastRunStartedAtUtc = config.LastRunStartedAtUtc,
                LastRunCompletedAtUtc = config.LastRunCompletedAtUtc,
                NextRunAtLocal = config.NextRunAtLocal,
                LastErrorCode = config.LastErrorCode,
                LastErrorMessage = config.LastErrorMessage,
                RequiresUserAction = config.RequiresUserAction
            };
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
