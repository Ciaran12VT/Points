using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;
using System.Collections.ObjectModel;

namespace Points.ViewModels.Settings
{
    public sealed class NotificationLogRowViewModel
    {
        private readonly NotificationLogModel _model;
        private readonly ITimeZoneService _timeZoneService;

        public NotificationLogRowViewModel(NotificationLogModel model, ITimeZoneService timeZoneService)
        {
            _model = model;
            _timeZoneService = timeZoneService;
        }

        public string TitleText => string.IsNullOrWhiteSpace(_model.CardTitle)
            ? $"Card {_model.CardId}"
            : _model.CardTitle;

        public string NoteText => string.IsNullOrWhiteSpace(_model.Note) ? "" : _model.Note;
        public bool HasNote => !string.IsNullOrWhiteSpace(_model.Note);
        public string Status => _model.Status;
        public string StatusColor => _model.Status switch
        {
            NotificationLogStatuses.Created => "#666666",
            NotificationLogStatuses.Scheduled => "#2D7DFF",
            NotificationLogStatuses.Sent => "#2E7D32",
            NotificationLogStatuses.Missed => "#B00020",
            _ => "#666666"
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
    }

    public sealed class NotificationLogViewModel : BindableObject
    {
        private static readonly TimeSpan MissedGracePeriod = TimeSpan.FromMinutes(15);

        private readonly INotificationLogService _notificationLogs;
        private readonly IClock _clock;
        private readonly ITimeZoneService _timeZoneService;
        private bool _isBusy;

        public ObservableCollection<NotificationLogRowViewModel> Rows { get; } = new();
        public Command RefreshCommand { get; }

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

        public bool IsEmpty => !IsBusy && Rows.Count == 0;

        public NotificationLogViewModel(INotificationLogService notificationLogs, IClock clock, ITimeZoneService? timeZoneService = null)
        {
            _notificationLogs = notificationLogs;
            _clock = clock;
            _timeZoneService = timeZoneService ?? new TimeZoneService();
            RefreshCommand = new Command(async () => await LoadAsync());
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                await _notificationLogs.MarkOverdueNotificationLogsMissedAsync(_clock.UtcNow, MissedGracePeriod);
                var logs = await _notificationLogs.GetNotificationLogsAsync();

                Rows.Clear();
                foreach (var log in logs)
                    Rows.Add(new NotificationLogRowViewModel(log, _timeZoneService));

                OnPropertyChanged(nameof(IsEmpty));
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
