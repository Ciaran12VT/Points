using System.Globalization;

namespace Points.Models
{
    public class AchievementCardModel : ObservableObject, ICardModel
    {
        public string Id { get; } = Guid.NewGuid().ToString();

        private string _title = "New Achievement";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _status = "In-Progress";
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private AchievementGoalType _goalType = AchievementGoalType.ActiveTime;
        public AchievementGoalType GoalType { get => _goalType; set => SetProperty(ref _goalType, value); }

        private double _targetValue = 1; // minutes if ActiveTime, points if Value, count if Steps
        public double TargetValue
        {
            get => _targetValue;
            set => SetProperty(ref _targetValue, value);
        }

        // For Steps: which step name + numeric target
        private string _stepName = "";
        public string StepName { get => _stepName; set => SetProperty(ref _stepName, value); }

        // For Achievements: which achievement title + numeric target (usually 1, but leaving flexible)
        private string _achievementTitle = "";
        public string AchievementTitle { get => _achievementTitle; set => SetProperty(ref _achievementTitle, value); }

        private AchievementCompletionType _completionType = AchievementCompletionType.Range;
        public AchievementCompletionType CompletionType { get => _completionType; set => SetProperty(ref _completionType, value); }

        // Range completion fields
        private AchievementRangeUnit _rangeUnit = AchievementRangeUnit.Days;
        public AchievementRangeUnit RangeUnit { get => _rangeUnit; set => SetProperty(ref _rangeUnit, value); }

        private int _rangeAmount = 7;
        public int RangeAmount { get => _rangeAmount; set => SetProperty(ref _rangeAmount, value); }

        // For now, store a deadline (even if Range); you’ll refine this when you build the details form.
        private DateTime? _deadline;
        public DateTime? Deadline { get => _deadline; set => SetProperty(ref _deadline, value); }

        // ---- Progress tracking (minimal for now) ----
        // We’ll keep a “current value” number you can update later from real sources
        // (active time, total value, step counts, etc).
        private double _currentValue;
        public double CurrentValue
        {
            get => _currentValue;
            set
            {
                if (SetProperty(ref _currentValue, value))
                {
                    RaisePropertyChanged(nameof(Progress));
                    RaisePropertyChanged(nameof(CurrentValueText));
                    RaisePropertyChanged(nameof(TargetText));
                    RaisePropertyChanged(nameof(CompletionTimeText));
                }
            }
        }

        // 0..1
        public double Progress
        {
            get
            {
                if (TargetValue <= 0) return 0;
                var p = CurrentValue / TargetValue;
                if (p < 0) return 0;
                if (p > 1) return 1;
                return p;
            }
        }

        // Labels the card needs
        public string ActiveTimeText
        {
            get
            {
                // For now only meaningful when GoalType == ActiveTime.
                // You’ll replace this with real active-time logic later.
                if (GoalType != AchievementGoalType.ActiveTime) return "Active: --:--:--";

                var minutes = CurrentValue;
                var ts = TimeSpan.FromMinutes(minutes);
                return $"Active: {ts:hh\\:mm\\:ss}";
            }
        }

        public string GoalTypeText => GoalType switch
        {
            AchievementGoalType.ActiveTime => "Goal: Active Time",
            AchievementGoalType.Value => "Goal: Value",
            AchievementGoalType.Steps => "Goal: Steps",
            _ => "Goal: ?"
        };

        public string CurrentValueText => $"Current: {CurrentValue.ToString("0.##", CultureInfo.InvariantCulture)}";

        public string TargetText
        {
            get
            {
                var v = TargetValue.ToString("0.##", CultureInfo.InvariantCulture);
                return GoalType switch
                {
                    AchievementGoalType.ActiveTime => $"Target: {v} min",
                    AchievementGoalType.Value => $"Target: {v}",
                    AchievementGoalType.Steps => $"Target: {v}",
                    _ => $"Target: {v}"
                };
            }
        }

        public string CompletionTimeText
        {
            get
            {
                if (CompletionType == AchievementCompletionType.Deadline)
                {
                    if (Deadline is null) return "Completion: (no deadline)";
                    return $"Completion: {Deadline.Value:G}";
                }

                // Range mode placeholder until you add real “minutes/hours/days/weeks/months” fields
                return "Completion: Range";
            }
        }

        public int Target { get; internal set; }
        public DateTime CompletedAt { get; internal set; }

        // For now: Achievements don’t contribute to global value until you define how they pay out.
        public double GetValue(DateTime start, DateTime end) => 0;

        // Call this when something time-based changes (later)
        public void NotifyTimeChanged()
        {
            // Minimal: just cause bindings to refresh if you’re updating CurrentValue elsewhere.
            RaisePropertyChanged(nameof(ActiveTimeText));
            RaisePropertyChanged(nameof(GoalTypeText));
            RaisePropertyChanged(nameof(CurrentValueText));
            RaisePropertyChanged(nameof(TargetText));
            RaisePropertyChanged(nameof(CompletionTimeText));
            RaisePropertyChanged(nameof(Progress));
        }
    }
}
