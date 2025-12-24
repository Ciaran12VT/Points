using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class MissionCardModel : ObservableObject, ICardModel
    {
        public string Id { get; } = Guid.NewGuid().ToString();

        private string _title = "Mission";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

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
            private set => SetProperty(ref _completedDate, value);
        }

        public Command CompleteCommand { get; }

        public bool IsAvailable => DateTime.Now >= AvailableFromDate;

        public bool IsPending => !IsComplete && DateTime.Now < AvailableFromDate;

        public string PendingWindowText
        {
            get
            {
                if (!IsPending)
                    return string.Empty;

                var now = DateTime.Now;

                var available = AvailableFromDate;
                var due = DueDate;

                var daysUntil = Math.Max(0, (available.Date - now.Date).Days);
                var durationDays = Math.Max(0, (due.Date - available.Date).Days);

                var availableText = available.ToString("ddd, MMM d");
                var dueText = due.ToString("ddd, MMM d");

                return $"{availableText} (in {daysUntil} days) - {dueText} (for {durationDays} days)";
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

        public void Complete(DateTime? completedAt = null)
        {
            if (IsComplete) return;

            IsComplete = true;
            Status = "Complete";
            CompletedDate = completedAt ?? DateTime.Now;

            CompleteCommand.ChangeCanExecute();
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
            if (end <= start) return 0;

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

    }
}
