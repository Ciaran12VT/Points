using Points.Services.Backup;
using Points.Services.Time;
using System.Collections.ObjectModel;

namespace Points.ViewModels.Settings
{
    public sealed class ScheduledBackupHistoryRowViewModel
    {
        private static readonly IReadOnlyDictionary<string, string> ResourceTitles =
            BackupPackageService.GetExportableResources()
                .ToDictionary(resource => resource.Key, resource => resource.Title, StringComparer.Ordinal);

        private readonly ScheduledBackupLogEntry _entry;
        private readonly ITimeZoneService _timeZoneService;

        public ScheduledBackupHistoryRowViewModel(
            ScheduledBackupLogEntry entry,
            ITimeZoneService timeZoneService)
        {
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        }

        public string StatusText => _entry.Status switch
        {
            ScheduledBackupRunStatus.RequiresUserAction => "Needs attention",
            _ => _entry.Status.ToString()
        };

        public string StatusColor => _entry.Status switch
        {
            ScheduledBackupRunStatus.Success => "#2E7D32",
            ScheduledBackupRunStatus.Failed => "#B00020",
            ScheduledBackupRunStatus.RequiresUserAction => "#B00020",
            ScheduledBackupRunStatus.Skipped => "#666666",
            _ => "#666666"
        };

        public string StartedAtText => TimeDisplayFormatter.FormatInstant(
            _entry.StartedAtUtc,
            "MMM-dd HH:mm",
            _timeZoneService);

        public string CompletedAtText => TimeDisplayFormatter.FormatNullableInstant(
            _entry.CompletedAtUtc,
            "MMM-dd HH:mm",
            "N/A",
            _timeZoneService);

        public string DurationText => FormatDuration(_entry.StartedAtUtc, _entry.CompletedAtUtc);

        public string DestinationText => string.IsNullOrWhiteSpace(_entry.DestinationDisplayName)
            ? _entry.DestinationType.ToString()
            : _entry.DestinationDisplayName;

        public string ResourcesText => FormatResources(_entry.ResourceKeys);
        public string FileNameText => _entry.FileName;
        public bool HasFileName => !string.IsNullOrWhiteSpace(_entry.FileName);
        public string FilePathText => _entry.FilePath ?? "";
        public bool HasFilePath => !string.IsNullOrWhiteSpace(_entry.FilePath);
        public string BytesText => FormatBytes(_entry.Bytes);
        public bool HasBytes => _entry.Bytes.HasValue;
        public string ErrorText => string.IsNullOrWhiteSpace(_entry.ErrorMessage)
            ? _entry.ErrorCode ?? ""
            : _entry.ErrorMessage;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

        private static string FormatResources(IReadOnlyList<string> keys)
        {
            if (keys.Count == 0)
                return "Database";

            return string.Join(
                ", ",
                keys.Select(key => ResourceTitles.TryGetValue(key, out var title) ? title : key));
        }

        private static string FormatDuration(DateTime startedAtUtc, DateTime? completedAtUtc)
        {
            if (!completedAtUtc.HasValue || completedAtUtc.Value < startedAtUtc)
                return "N/A";

            var duration = completedAtUtc.Value - startedAtUtc;
            if (duration.TotalSeconds < 60)
                return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))}s";

            if (duration.TotalMinutes < 60)
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";

            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        private static string FormatBytes(long? bytes)
        {
            if (!bytes.HasValue)
                return "";

            double value = bytes.Value;
            string[] units = { "B", "KB", "MB", "GB" };
            var unitIndex = 0;

            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return unitIndex == 0
                ? $"{bytes.Value} {units[unitIndex]}"
                : $"{value:0.##} {units[unitIndex]}";
        }
    }

    public sealed class ScheduledBackupHistoryViewModel : BindableObject
    {
        private const int MaxHistoryEntries = 50;

        private readonly IScheduledBackupLogStore _logStore;
        private readonly ITimeZoneService _timeZoneService;
        private bool _isBusy;

        public ScheduledBackupHistoryViewModel(
            IScheduledBackupLogStore logStore,
            ITimeZoneService timeZoneService)
        {
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
            RefreshCommand = new Command(async () => await LoadAsync());
        }

        public ObservableCollection<ScheduledBackupHistoryRowViewModel> Rows { get; } = new();
        public Command RefreshCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsEmpty => !IsBusy && Rows.Count == 0;

        public async Task LoadAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            try
            {
                var entries = await _logStore.GetRecentAsync(MaxHistoryEntries);

                Rows.Clear();
                foreach (var entry in entries)
                    Rows.Add(new ScheduledBackupHistoryRowViewModel(entry, _timeZoneService));

                OnPropertyChanged(nameof(IsEmpty));
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
