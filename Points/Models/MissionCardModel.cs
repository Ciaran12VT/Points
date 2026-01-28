using Points.Evaluators;
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

        private string _title = "Mission";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public List<TimeValueAchievementEvaluator> TimeValueAchievementEvaluators { get; set; }

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

        private DateTime _createdDate = DateTime.Now;
        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }

        private DateTime _availableFromDate = DateTime.Today;
        public DateTime AvailableFromDate
        {
            get => _availableFromDate;
            set => SetProperty(ref _availableFromDate, value);
        }

        private DateTime _dueDate = DateTime.Today.AddDays(1);
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
            string retVal = value.ToString("yyyy-mm-dd HH:mm");

            if (value < DateTime.Now && value >= DateTime.Today)
            {
                retVal = $"OVERDUE: Today @ {value.ToString("hh:mm")}{(value.Hour > 12 ? "pm" : "am")}";
            }
            else if (value < DateTime.Now)
            {
                retVal = $"Event Time: Today @ {value.ToString("hh:mm")}{(value.Hour > 12 ? "pm" : "am")}";
            }
            else if (value < DateTime.Now.AddDays(1))
            {
                retVal = $"Event Time: Tomorrow @ {value.ToString("hh:mm")}{(value.Hour > 12 ? "pm" : "am")}";
            }
            else if (value < DateTime.Now.AddDays(7))
            {
                retVal = $"Event Time: {value.DayOfWeek} @ {value.ToString("hh:mm")}{(value.Hour > 12 ? "pm" : "am")}";
            }
            else
            {
                retVal = $"Event Time: {value.ToString("MMM-dd")} @ {value.ToString("hh:mm")}{(value.Hour > 12 ? "pm" : "am")}";
            }

            return retVal;
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

        public bool IsAvailable => DateTime.Now >= AvailableFromDate;

        public bool IsPending => !IsComplete && DateTime.Now < AvailableFromDate;

        public string SubTypeLabelColor => SubType == MissionSubType.Stable ? "LightBlue" : (SubType == MissionSubType.Degrade ? "DarkOrange" : "DarkRed");

        public string PendingWindowText
        {
            get
            {
                if (!IsPending) return string.Empty;

                var now = DateTime.Now;

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

        public ICommand ToggleActivityCommand { get; }

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
            CompletedDate = failedAt ?? DateTime.Now;

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
            ToggleActivityCommand = new Command(ToggleActivity);
        }

        private void ToggleActivity()
        {
            var now = DateTime.Now;

            var valueRate = new ValueRateModel() { RateName = "Base Rate", ValuePerMinute = ValuePerMinute };

            if (!IsActive)
            {
                // Start: store (start, DateTime.MinValue) to mean "open interval"          
                Activity.Add(new ActivityModel(now, DateTime.MinValue, valueRate.RateName, valueRate.ValuePerMinute));
                IsActive = true;
                RaisePropertyChanged(nameof(Activity));
                return;
            }

            // Stop: close the most recent open interval
            for (int i = Activity.Count - 1; i >= 0; i--)
            {
                if (Activity[i].EndDate == DateTime.MinValue)
                {
                    Activity[i].EndDate = now;
                    IsActive = false;
                    RaisePropertyChanged(nameof(Activity));
                    return;
                }
            }

            // If we got here, state was inconsistent; recover by starting a new interval
            Activity.Add(new ActivityModel(now, DateTime.MinValue, valueRate.RateName, valueRate.ValuePerMinute));
            IsActive = true;
            RaisePropertyChanged(nameof(Activity));
        }

        public void Activitate()
        {
            IsActive = true;
            RaisePropertyChanged(nameof(Activity));
            return;
        }

        public void StopActivity()
        {
            if (!IsActive) return;

            var now = DateTime.Now;

            for (int i = Activity.Count - 1; i >= 0; i--)
            {
                if (Activity[i].EndDate == DateTime.MinValue)
                {
                    Activity[i].EndDate = now;
                    IsActive = false;
                    RaisePropertyChanged(nameof(Activity));
                    return;
                }
            }

            // If somehow there was no open interval, just mark inactive.
            IsActive = false;
        }


        public TimeSpan GetActiveTime(DateTime start, DateTime end)
        {
            if (end <= start) return TimeSpan.Zero;

            double totalMinutes = 0;

            foreach (var period in Activity)
            {
                var aStart = period.StartDate;
                var aEnd = period.EndDate == DateTime.MinValue ? Min(end, DateTime.Now) : period.EndDate;

                //var overlapStart = aStart > start ? aStart : start;
                //var overlapEnd = aEnd < end ? aEnd : end;

                //if (overlapEnd > overlapStart)
                //    totalMinutes += (overlapEnd - overlapStart).TotalMinutes;

                totalMinutes += (aEnd - aStart).TotalMinutes;
            }

            return TimeSpan.FromMinutes(totalMinutes);
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
            var effectiveEnd = CompletedDate is DateTime completed ? (completed < end ? completed : end) : end;

            // Stable / Degrade: one-off at completion moment (if completion within window)
            if (SubType == MissionSubType.Stable || SubType == MissionSubType.Degrade)
            {
                if (CompletedDate is not DateTime c) return 0;
                if (c < start || c > end) return 0;

                return SubType == MissionSubType.Stable
                    ? Value
                    : GetCompletionValueAt(c);
            }

            // Rot: ongoing penalty once overdue until completion (or end)
            // No penalty before DueDate
            var penaltyStart = Max(start, DueDate);
            var penaltyEnd = CompletedDate != null ? CompletedDate.Value : DateTime.Now;

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
            if (end <= start) return 0;

            double totalValue = 0;

            foreach (var period in Activity)
            {
                var aStart = period.StartDate;
                var aEnd = period.EndDate == DateTime.MinValue ? Min(end, DateTime.Now) : period.EndDate;

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

        public double GetCurrentValue(DateTime now)
        {
            // Completed → frozen at completion time
            if (IsComplete && CompletedDate is DateTime completed)
                return GetCompletionValueAt(completed);

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
            CompletedDate = completedAt ?? DateTime.Now;

            CompleteCommand.ChangeCanExecute();
        }

        public DateTime GetLastActiveTime()
        {
            if (IsActive) return DateTime.Now;

            if (Activity.Count == 0) return DateTime.MinValue;

            return Activity.Select(x => x.EndDate).Max();
        }
    }
}
