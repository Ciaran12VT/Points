using System.Collections.ObjectModel;
using System.Globalization;

namespace Points.Models
{

    public class TrophyItem
    {
        public string DisplayName { get; set; } = "";
        public string LocalPath { get; set; } = "";
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }

    public enum AchievementDifficultyLevels
    {
        Easy, Medium, Hard, Ridiculous, Special
    }

    public class AchievementCardModel : ObservableObject, ICardModel
    {
        public AchievementCardModel()
        {
            Trophies.CollectionChanged += (_, __) => RaisePropertyChanged(nameof(TrophyCount));
        }

        private string _title = "New Achievement";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _status = "In-Progress";
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private AchievementGoalType _goalType = AchievementGoalType.ActiveTime;
        public AchievementGoalType GoalType
        {
            get => _goalType;
            set
            {
                if (SetProperty(ref _goalType, value))
                {
                    RaisePropertyChanged(nameof(GoalTypeText));
                    RaisePropertyChanged(nameof(TargetText));
                    RaisePropertyChanged(nameof(ActiveTimeText));
                }
            }
        }

        private double _targetValue = 1;
        public double TargetValue
        {
            get => _targetValue;
            set
            {
                if (SetProperty(ref _targetValue, value))
                {
                    RaisePropertyChanged(nameof(TargetText));
                    RaisePropertyChanged(nameof(Progress));
                }
            }
        }

        private string _activeTimeTargetText = "";
        public string ActiveTimeTargetText
        {
            get => _activeTimeTargetText;
            set
            {
                if (SetProperty(ref _activeTimeTargetText, value))
                {
                    RaisePropertyChanged(nameof(TargetText));
                    RaisePropertyChanged(nameof(ActiveTimeText));
                }
            }
        }

        private DateTime? _lastEarnedAt;
        public DateTime? LastEarnedAt
        {
            get => _lastEarnedAt;
            set
            {
                if (SetProperty(ref _lastEarnedAt, value))
                {
                    RaisePropertyChanged(nameof(IsLockedThisRange));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CardBackgroundColor));
                }
            }
        }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                SetProperty(ref _isPinned, value);
            }
        }

        public bool IsLockedThisRange
        {
            get
            {
                if (CompletionType != AchievementCompletionType.Range)
                    return false;

                if (LastEarnedAt is null)
                    return false;

                var now = DateTime.Now;
                var windowStart = GetRangeWindowStart(now);

                // If it was earned within the window, it is locked.
                return LastEarnedAt.Value >= windowStart && LastEarnedAt.Value <= now;
            }
        }

        private DateTime GetRangeWindowStart(DateTime now)
        {
            // RangeAmount is int, RangeUnit is enum (you already have these).
            var amt = RangeAmount;

            return RangeUnit switch
            {
                AchievementRangeUnit.Minutes => now.AddMinutes(-amt),
                AchievementRangeUnit.Hours => now.AddHours(-amt),
                AchievementRangeUnit.Days => now.AddDays(-amt),
                AchievementRangeUnit.Weeks => now.AddDays(-(7 * amt)),
                AchievementRangeUnit.Months => now.AddMonths(-amt),
                _ => now.AddDays(-amt)
            };
        }

        public string StatusDisplay
        {
            get
            {
                if (IsLockedThisRange)
                    return "Locked";
                return Status;
            }
        }

        public Color CardBackgroundColor => IsLockedThisRange ? Color.FromArgb("#2A2A2A") : GetBackColorBasedOnDifficulty();

        public Color CardForeColor => IsLockedThisRange ? Colors.White : GetForeColorBasedOnDifficulty();

        private Color GetBackColorBasedOnDifficulty()
        {
            switch (Difficulty)
            {
                case AchievementDifficultyLevels.Easy:
                    return Colors.White;
                    break;
                case AchievementDifficultyLevels.Medium:
                    return Colors.LightGreen;
                    break;
                case AchievementDifficultyLevels.Hard:
                    return Colors.Brown;
                    break;
                case AchievementDifficultyLevels.Ridiculous:
                    return Colors.Black;
                    break;
                case AchievementDifficultyLevels.Special:
                    return Colors.DarkBlue;
                    break;
                default:
                    return Colors.White;
                    break;
            }
        }

        private Color GetForeColorBasedOnDifficulty()
        {
            switch (Difficulty)
            {
                case AchievementDifficultyLevels.Easy:
                    return Colors.Black;
                    break;
                case AchievementDifficultyLevels.Medium:
                    return Colors.Black;
                    break;
                case AchievementDifficultyLevels.Hard:
                    return Colors.White;
                    break;
                case AchievementDifficultyLevels.Ridiculous:
                    return Colors.White;
                    break;
                case AchievementDifficultyLevels.Special:
                    return Colors.White;
                    break;
                default:
                    return Colors.Black;
                    break;
            }
        }

        private AchievementDifficultyLevels _difficulty = AchievementDifficultyLevels.Easy;
        public AchievementDifficultyLevels Difficulty
        {
            get => _difficulty;
            set
            {
                if (SetProperty(ref _difficulty, value))
                {
                    RaisePropertyChanged(nameof(Difficulty));
                    RaisePropertyChanged(nameof(CardBackgroundColor));
                    RaisePropertyChanged(nameof(CardForeColor));
                }
            }
        }


        // For Steps: which step name + numeric target
        private string _stepName = "";
        public string StepName { get => _stepName; set => SetProperty(ref _stepName, value); }

        // For Achievements: which achievement title + numeric target (usually 1, but leaving flexible)
        private string _achievementTitle = "";
        public string AchievementTitle { get => _achievementTitle; set => SetProperty(ref _achievementTitle, value); }

        private AchievementCompletionType _completionType = AchievementCompletionType.Range;
        public AchievementCompletionType CompletionType
        {
            get => _completionType;
            set
            {
                if (SetProperty(ref _completionType, value))
                {
                    RaisePropertyChanged(nameof(IsLockedThisRange));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CardBackgroundColor));
                }
            }
        }

        // Range completion fields
        private AchievementRangeUnit _rangeUnit = AchievementRangeUnit.Days;
        public AchievementRangeUnit RangeUnit
        {
            get => _rangeUnit;
            set
            {
                if (SetProperty(ref _rangeUnit, value))
                {
                    RaisePropertyChanged(nameof(IsLockedThisRange));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CardBackgroundColor));
                }
            }
        }

        private int _rangeAmount = 7;
        public int RangeAmount
        {
            get => _rangeAmount;
            set
            {
                if (SetProperty(ref _rangeAmount, value))
                {
                    RaisePropertyChanged(nameof(IsLockedThisRange));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CardBackgroundColor));
                }
            }
        }

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
                    AchievementGoalType.ActiveTime => $"Target: {ActiveTimeTargetText}",
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

        public ObservableCollection<string> Trophies { get; } = new();

        public int TrophyCount => Trophies.Count;

        public int Id { get; set; }


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
