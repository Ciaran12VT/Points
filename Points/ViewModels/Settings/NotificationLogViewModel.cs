using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Points.ViewModels.Settings
{
    public sealed class NotificationLogRowViewModel : BindableObject
    {
        private readonly NotificationLogModel _model;
        private readonly ITimeZoneService _timeZoneService;

        public NotificationLogRowViewModel(NotificationLogModel model, ITimeZoneService timeZoneService)
        {
            _model = model;
            _timeZoneService = timeZoneService;
        }

        public long NotificationLogId => _model.NotificationLogId;

        public string TitleText => string.IsNullOrWhiteSpace(_model.CardTitle)
            ? $"Card {_model.CardId}"
            : _model.CardTitle;

        public string NoteText => string.IsNullOrWhiteSpace(_model.Note) ? "" : _model.Note;
        public bool HasNote => !string.IsNullOrWhiteSpace(_model.Note);
        public string Status => _model.Status;
        public string StatusColor => _model.Status switch
        {
            NotificationLogStatuses.Created => NotificationLogStatusColors.Created,
            NotificationLogStatuses.Scheduled => NotificationLogStatusColors.Scheduled,
            NotificationLogStatuses.Sent => NotificationLogStatusColors.Sent,
            NotificationLogStatuses.Missed => NotificationLogStatusColors.Missed,
            NotificationLogStatuses.MissedSeen => NotificationLogStatusColors.MissedSeen,
            _ => NotificationLogStatusColors.Created
        };

        public string CreatedAtText => Format(_model.CreatedAt);
        public string ScheduledAtText => Format(_model.ScheduledAt);
        public string ScheduleForText => Format(_model.ScheduleFor);
        public string SentAtText => Format(_model.SentAt);
        public string ErrorText => _model.Error ?? "";
        public bool HasError => !string.IsNullOrWhiteSpace(_model.Error);

        private string Format(DateTime value)
        {
            return TimeDisplayFormatter.FormatInstant(value, "MMM-dd HH:mm", _timeZoneService);
        }

        private string Format(DateTime? value) => value.HasValue
            ? TimeDisplayFormatter.FormatInstant(value.Value, "MMM-dd HH:mm", _timeZoneService)
            : "N/A";

        public void MarkMissedSeen(DateTime updatedAt)
        {
            if (_model.Status != NotificationLogStatuses.Missed)
                return;

            _model.Status = NotificationLogStatuses.MissedSeen;
            _model.UpdatedAt = updatedAt;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusColor));
        }
    }

    public sealed class NotificationLogTabViewModel : BindableObject
    {
        private readonly bool _showBadge;
        private readonly string _badgeColor;
        private int _count;
        private bool _isSelected;

        public NotificationLogTabViewModel(
            NotificationLogFilter filter,
            string title,
            bool showBadge,
            string badgeColor)
        {
            Filter = filter;
            Title = title;
            _showBadge = showBadge;
            _badgeColor = badgeColor;
        }

        public NotificationLogFilter Filter { get; }
        public string Title { get; }
        public ObservableCollection<NotificationLogRowViewModel> Rows { get; } = new();
        public bool HasLoaded { get; set; }
        public bool IsFullyLoaded { get; set; }
        public bool IsLoading { get; set; }

        public int Count
        {
            get => _count;
            private set
            {
                if (_count == value)
                    return;

                _count = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasBadge));
                OnPropertyChanged(nameof(BadgeText));
            }
        }

        public bool HasBadge => _showBadge && Count > 0;
        public string BadgeText => Count.ToString(CultureInfo.InvariantCulture);
        public string BadgeColor => _badgeColor;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(BorderColor));
                OnPropertyChanged(nameof(TextColor));
            }
        }

        public string BackgroundColor => IsSelected ? "#111111" : "Transparent";
        public string BorderColor => IsSelected ? "#111111" : "#DDDDDD";
        public string TextColor => IsSelected ? "#FFFFFF" : "#666666";

        public void SetCount(int count)
        {
            Count = Math.Max(0, count);
        }

        public void Reset()
        {
            Rows.Clear();
            HasLoaded = false;
            IsFullyLoaded = false;
            IsLoading = false;
        }
    }

    public sealed class NotificationLogViewModel : BindableObject
    {
        private static readonly TimeSpan MissedGracePeriod = TimeSpan.FromMinutes(15);
        private const int PageSize = 30;

        private readonly INotificationLogService _notificationLogs;
        private readonly IClock _clock;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ObservableCollection<NotificationLogRowViewModel> _emptyRows = new();
        private bool _isBusy;
        private bool _isLoadingMore;
        private NotificationLogTabViewModel? _selectedTab;

        public ObservableCollection<NotificationLogTabViewModel> Tabs { get; } = new();
        public ObservableCollection<NotificationLogRowViewModel> Rows => SelectedTab?.Rows ?? _emptyRows;
        public Command RefreshCommand { get; }
        public Command<NotificationLogTabViewModel> SelectTabCommand { get; }
        public Command LoadMoreCommand { get; }

        public NotificationLogTabViewModel? SelectedTab
        {
            get => _selectedTab;
            private set
            {
                if (ReferenceEquals(_selectedTab, value))
                    return;

                if (_selectedTab != null)
                    _selectedTab.IsSelected = false;

                _selectedTab = value;

                if (_selectedTab != null)
                    _selectedTab.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            private set
            {
                if (_isLoadingMore == value)
                    return;

                _isLoadingMore = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsEmpty => !IsBusy &&
            !IsLoadingMore &&
            SelectedTab?.HasLoaded == true &&
            Rows.Count == 0;

        public NotificationLogViewModel(INotificationLogService notificationLogs, IClock clock, ITimeZoneService? timeZoneService = null)
        {
            _notificationLogs = notificationLogs;
            _clock = clock;
            _timeZoneService = timeZoneService ?? new TimeZoneService();
            Tabs.Add(new NotificationLogTabViewModel(
                NotificationLogFilter.Scheduled,
                "Scheduled",
                showBadge: true,
                NotificationLogStatusColors.Scheduled));
            Tabs.Add(new NotificationLogTabViewModel(
                NotificationLogFilter.Missed,
                "Missed",
                showBadge: true,
                NotificationLogStatusColors.Missed));
            Tabs.Add(new NotificationLogTabViewModel(
                NotificationLogFilter.History,
                "Sent / Seen",
                showBadge: false,
                NotificationLogStatusColors.Sent));

            SelectedTab = Tabs[0];
            RefreshCommand = new Command(async () => await LoadAsync());
            SelectTabCommand = new Command<NotificationLogTabViewModel>(async tab => await SelectTabAsync(tab));
            LoadMoreCommand = new Command(async () => await LoadMoreAsync());
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                await _notificationLogs.MarkOverdueNotificationLogsMissedAsync(_clock.UtcNow, MissedGracePeriod);
                foreach (var tab in Tabs)
                    tab.Reset();

                await RefreshTabCountsAsync();
                await SelectTabInternalAsync(SelectedTab ?? Tabs[0], loadIfNeeded: true);
                OnPropertyChanged(nameof(IsEmpty));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SelectTabAsync(NotificationLogTabViewModel? tab)
        {
            if (tab == null || IsBusy)
                return;

            await SelectTabInternalAsync(tab, loadIfNeeded: true);
        }

        private async Task SelectTabInternalAsync(NotificationLogTabViewModel tab, bool loadIfNeeded)
        {
            SelectedTab = tab;

            if (loadIfNeeded && !tab.HasLoaded)
                await LoadNextPageAsync(tab);
        }

        private async Task LoadMoreAsync()
        {
            if (SelectedTab == null || IsBusy)
                return;

            await LoadNextPageAsync(SelectedTab);
        }

        private async Task LoadNextPageAsync(NotificationLogTabViewModel tab)
        {
            if (tab.IsLoading || tab.IsFullyLoaded)
                return;

            tab.IsLoading = true;
            IsLoadingMore = true;

            try
            {
                var offset = tab.Filter == NotificationLogFilter.Missed ? 0 : tab.Rows.Count;
                var logs = await _notificationLogs.GetNotificationLogsAsync(tab.Filter, offset, PageSize);
                var addedRows = logs
                    .Select(log => new NotificationLogRowViewModel(log, _timeZoneService))
                    .ToList();

                foreach (var row in addedRows)
                    tab.Rows.Add(row);

                tab.HasLoaded = true;
                tab.IsFullyLoaded = logs.Count < PageSize;

                if (tab.Filter == NotificationLogFilter.Missed)
                    await MarkAddedMissedRowsSeenAsync(tab, addedRows);

                OnPropertyChanged(nameof(IsEmpty));
            }
            finally
            {
                tab.IsLoading = false;
                IsLoadingMore = false;
            }
        }

        private async Task MarkAddedMissedRowsSeenAsync(
            NotificationLogTabViewModel missedTab,
            IReadOnlyList<NotificationLogRowViewModel> addedRows)
        {
            var missedRows = addedRows
                .Where(row => row.Status == NotificationLogStatuses.Missed)
                .ToList();

            if (missedRows.Count == 0)
                return;

            var seenAt = _clock.UtcNow;
            await _notificationLogs.MarkNotificationLogsMissedSeenAsync(
                missedRows.Select(row => row.NotificationLogId),
                seenAt);

            foreach (var row in missedRows)
                row.MarkMissedSeen(seenAt);

            await RefreshTabCountsAsync();

            var historyTab = Tabs.FirstOrDefault(tab => tab.Filter == NotificationLogFilter.History);
            if (historyTab?.HasLoaded == true)
                historyTab.Reset();

            missedTab.IsFullyLoaded = missedTab.Count == 0;
        }

        private async Task RefreshTabCountsAsync()
        {
            foreach (var tab in Tabs)
            {
                if (tab.Filter is NotificationLogFilter.Scheduled or NotificationLogFilter.Missed)
                    tab.SetCount(await _notificationLogs.GetNotificationLogCountAsync(tab.Filter));
            }
        }
    }
}
