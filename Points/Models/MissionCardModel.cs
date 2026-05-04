
using Points.Evaluators;
using Points.Services.Activity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.Models
{
    public class MissionCardModel : ObservableObject, IActiveCardModel
    {
        public int Id { get; set; }
        public long CardID { get; set; }
        public Guid MissionGuid { get; set; } = Guid.NewGuid();
        public int DisplayOrder { get; set; }

        private string _title = "Mission";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public event Action<IActiveCardModel>? LongPressRequested;

        public void FireLongPressRequested(IActiveCardModel card)
        {
            LongPressRequested?.Invoke(card);
        }

        public List<TimeValueAchievementEvaluator> TimeValueAchievementEvaluators { get; set; } = new();

        public List<LockModel> Locks { get; set; } = new();

        private string _status = "In-Progress";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _tags = "#Test, #Other";
        public string Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string? _sharedWith;
        public string? SharedWith
        {
            get => _sharedWith;
            set => SetProperty(ref _sharedWith, value);
        }

        private MissionSubType _subType = MissionSubType.Stable;
        public MissionSubType SubType
        {
            get => _subType;
            set => SetProperty(ref _subType, value);
        }

        private double _value = 25;
        public double Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        private bool _isComplete;
        public bool IsComplete
        {
            get => _isComplete;
            private set => SetProperty(ref _isComplete, value);
        }

        private DateTime _createdDate = ActivityTimeMath.UtcNow;
        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }

        private DateTime _availableFromDate = ActivityTimeMath.LocalNow.Date;
        public DateTime AvailableFromDate
        {
            get => _availableFromDate;
            set => SetProperty(ref _availableFromDate, value);
        }

        private DateTime _dueDate = ActivityTimeMath.LocalNow.Date.AddDays(1);
        public DateTime DueDate
        {
            get => _dueDate;
            set => SetProperty(ref _dueDate, value);
        }

        private DateTime? _completedDate;
        public DateTime? CompletedDate
        {
            get => _completedDate;
            set => SetProperty(ref _completedDate, value);
        }

        // NEW: EventDate
        private DateTime? _eventDate;
        public DateTime? EventDate
        {
            get => _eventDate;
            set
            {
                if (SetProperty(ref _eventDate, value))
                {
                    RaisePropertyChanged(nameof(HasEventDate));
                    RaisePropertyChanged(nameof(EventDateTimeText));
                }
            }
        }

        public bool HasEventDate => EventDate.HasValue;

        public string EventDateTimeText =>
            EventDate.HasValue
                ? GetEventDateText(EventDate.Value)  // tweak format if you prefer
                : string.Empty;

        private string GetEventDateText(DateTime value)
        {
            var now = ActivityTimeMath.LocalNow;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var time = value.ToString("hh:mmtt").ToLower();

            if (value < now && value.Date == today)
                return $"OVERDUE: Today @ {time}";

            if (value.Date == today)
                return $"Event Time: Today @ {time}";

            if (value.Date == tomorrow)
                return $"Event Time: Tomorrow @ {time}";

            if (value < now.AddDays(7))
                return $"Event Time: {value:dddd} @ {time}";

            return $"Event Time: {value:MMM-dd} @ {time}";
        }

        private TimeSpan? _estCompletionTime;
        public TimeSpan? EstCompletionTime
        {
            get => _estCompletionTime;
            set => SetProperty(ref _estCompletionTime, value);
        }

        public string EstCompletionTimeText
        {
            get
            {
                if (!EstCompletionTime.HasValue) return "00:00:00";

                var totalHours = (int)EstCompletionTime.Value.TotalHours;
                var formatted = $"{totalHours}:{EstCompletionTime.Value.Minutes:D2}:{EstCompletionTime.Value.Seconds:D2}";

                return formatted;
            }
        }

        private TimeSpan? _activeTime;
        public TimeSpan? ActiveTime
        {
            get => _activeTime;
            set => SetProperty(ref _activeTime, value);
        }

        public string ActiveTimeText
        {
            get
            {
                if (!ActiveTime.HasValue) return "00:00:00";

                var totalHours = (int)ActiveTime.Value.TotalHours;
                var formatted = $"{totalHours}:{ActiveTime.Value.Minutes:D2}:{ActiveTime.Value.Seconds:D2}";

                return formatted;
            }
        }

        public Command CompleteCommand { get; }

        public bool IsAvailable => ActivityTimeMath.LocalNow >= AvailableFromDate;

        public bool IsPending => !IsComplete && ActivityTimeMath.LocalNow < AvailableFromDate;

        public string SubTypeLabelColor => SubType == MissionSubType.Stable ? "LightBlue" : (SubType == MissionSubType.Degrade ? "DarkOrange" : "DarkRed");

        public string PendingWindowText
        {
            get
            {
                if (!IsPending) return string.Empty;

                var now = ActivityTimeMath.LocalNow;

                var available = AvailableFromDate;
                var due = DueDate;

                var daysUntil = Math.Max(0, (available.Date - now.Date).Days);
                var durationDays = Math.Max(0, (due.Date - available.Date).Days);

                var availableText = available.ToString("ddd, MMM d");
                var dueText = due.ToString("ddd, MMM d");

                return $"For {durationDays} day" + (durationDays == 1 ? "" : "s");
            }
        }

        public string StatusDisplay
        {
            get
            {
                if (IsPending)
                    return "Pending";

                return Status;
            }
        }

        private bool _isFailed;
        public bool IsFailed
        {
            get => _isFailed;
            set => SetProperty(ref _isFailed, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
        public List<ActivityModel> Activity { get; set; } = new();

        private double _valuePerMinute = 0;
        public double ValuePerMinute
        {
            get => _valuePerMinute;
            set => SetProperty(ref _valuePerMinute, value);
        }

        public void Fail(DateTime? failedAt = null)
        {
            if (IsFailed)
                return;

            IsFailed = true;
            Status = "Failed";

            IsComplete = true;
            CompletedDate = failedAt ?? ActivityTimeMath.UtcNow;

            CompleteCommand.ChangeCanExecute();
        }


        public void NotifyTimeChanged()
        {
            RaisePropertyChanged(nameof(IsAvailable));
            RaisePropertyChanged(nameof(IsPending));
            RaisePropertyChanged(nameof(PendingWindowText));
        }

        public MissionCardModel()
        {
            CompleteCommand = new Command(() => Complete(), () => !IsComplete);
        }

        public TimeSpan GetActiveTime(DateTime start, DateTime end)
        {
            return ActivityIntervalCalculator.GetActiveTimeInRange(Activity, start, end, ActivityTimeMath.UtcNow);
        }

        private double GetCompletionValueAt(DateTime t)
        {
            // If due <= available, treat as immediate (avoid divide-by-zero)
            if (DueDate <= AvailableFromDate)
                return Value;

            if (SubType == MissionSubType.Stable)
                return Value;

            var totalMinutes = (DueDate - AvailableFromDate).TotalMinutes;
            var elapsedMinutes = (t - AvailableFromDate).TotalMinutes;

            // Linear: Value at AvailableFromDate, 0 at DueDate
            var slope = Value / totalMinutes; // points per minute
            var v = Value - (slope * elapsedMinutes);

            if (SubType == MissionSubType.Degrade)
                return Math.Max(0, v);

            // Rot: allow negative
            return v;
        }

        public double GetValue(DateTime start, DateTime end)
        {
            return GetPrizeValue(start, end) + GetValueFromValuePerMinute(start, end);
        }

        public double GetPrizeValue(DateTime start, DateTime end)
        {
            if (end <= start) return 0;

            if (IsFailed) return Value * -1;

            // Only count up to completion time (if completed), otherwise up to 'end'
            var completedLocal = CompletedDate is DateTime completed
                ? ToLocalWallClock(completed)
                : (DateTime?)null;
            var effectiveEnd = completedLocal is DateTime completedAt ? (completedAt < end ? completedAt : end) : end;

            // Stable / Degrade: one-off at completion moment (if completion within window)
            if (SubType == MissionSubType.Stable || SubType == MissionSubType.Degrade)
            {
                if (completedLocal is not DateTime c) return 0;
                if (c < start || c > end) return 0;

                return SubType == MissionSubType.Stable
                    ? Value
                    : GetCompletionValueAt(c);
            }

            // Rot: ongoing penalty once overdue until completion (or end)
            // No penalty before DueDate
            var penaltyStart = Max(start, DueDate);
            var penaltyEnd = completedLocal ?? ActivityTimeMath.LocalNow;

            if (penaltyEnd <= penaltyStart) return 0;

            // points-per-minute slope based on AvailableFromDate -> DueDate duration
            if (DueDate <= AvailableFromDate) return 0; // safety

            var totalMinutes = (DueDate - AvailableFromDate).TotalMinutes;
            if (totalMinutes <= 0) return 0;

            var slope = Value / totalMinutes;              // points per minute
            var minutesOverdue = (penaltyEnd - penaltyStart).TotalMinutes;

            // Negative stream
            return -slope * minutesOverdue;
        }

        public virtual double GetValueFromValuePerMinute(DateTime start, DateTime end)
        {
            start = ActivityTimeMath.ToUtcAssumingLocal(start);
            end = ActivityTimeMath.ToUtcAssumingLocal(end);

            if (end <= start) return 0;

            double totalValue = 0;

            foreach (var period in Activity)
            {
                var aStart = ActivityTimeMath.ToUtcAssumingLocal(period.StartDate);
                var aEnd = !period.EndDate.HasValue
                    ? Min(end, ActivityTimeMath.UtcNow)
                    : ActivityTimeMath.ToUtcAssumingLocal(period.EndDate.Value);

                var overlapStart = aStart > start ? aStart : start;
                var overlapEnd = aEnd < end ? aEnd : end;

                if (overlapEnd > overlapStart)
                {
                    var totalMinutes = (overlapEnd - overlapStart).TotalMinutes;

                    double currentRate = period.ValuePerMinute;

                    totalValue += currentRate * totalMinutes;
                }

            }

            return totalValue;
        }

        // helpers
        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
        private static DateTime ToLocalWallClock(DateTime value) => value.Kind == DateTimeKind.Utc
            ? value.ToLocalTime()
            : value;

        public double GetCurrentValue(DateTime now)
        {
            // Completed → frozen at completion time
            if (IsComplete && CompletedDate is DateTime completed)
                return GetCompletionValueAt(ToLocalWallClock(completed));

            // Not yet available → no value
            if (now < AvailableFromDate)
                return 0;

            // Incomplete
            return GetCompletionValueAt(now);
        }

        public void Complete(DateTime? completedAt = null)
        {
            if (IsComplete) return;

            IsComplete = true;
            Status = "Complete";
            CompletedDate = completedAt ?? ActivityTimeMath.UtcNow;

            CompleteCommand.ChangeCanExecute();
        }

        public DateTime GetLastActiveTime()
        {
            if (IsActive) return ActivityTimeMath.UtcNow;

            if (Activity.Count == 0) return DateTime.MinValue;

            return Activity
                .Select(x => x.EndDate.HasValue ? ActivityTimeMath.ToUtcAssumingLocal(x.EndDate.Value) : ActivityTimeMath.UtcNow)
                .Max();
        }
    }
}
