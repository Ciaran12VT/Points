using System.Collections.ObjectModel;
using System.Globalization;

namespace Points.Models
{

    public class TrophyItem
    {
        public string DisplayName { get; set; } = "";
        public string LocalPath { get; set; } = "";
        public DateTime AddedAt { get; set; } = ActivityTimeMath.LocalNow;
    }

    public enum AchievementDifficultyLevels
    {
        Easy, Medium, Hard, Ridiculous, Special
    }

    public class AchievementCardModel : ObservableObject, ICardModel
    {
        public AchievementCardModel()
        {
            _createdDate = ActivityTimeMath.LocalNow;
            _trophies.CollectionChanged += OnTrophiesCollectionChanged;
        }

        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        
        private string _title = "New Achievement";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _status = "In-Progress";
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private AchievementTargetType _targetType = AchievementTargetType.ActiveTime;
        public AchievementTargetType TargetType
        {
            get => _targetType;
            set
            {
                if (SetProperty(ref _targetType, value))
                {
                    RaisePropertyChanged(nameof(TargetTypeText));
                    RaisePropertyChanged(nameof(TargetText));
                    RaisePropertyChanged(nameof(ActiveTimeText));
                }
            }
        }


        private DateTime _createdDate;
        public DateTime CreatedDate
        {
            get => _createdDate;
            set
            {
                if (SetProperty(ref _createdDate, value))
                {
                    RaisePropertyChanged(nameof(HasStarted));
                    RaisePropertyChanged(nameof(IsPending));
                    RaisePropertyChanged(nameof(IsInProgress));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CompletionTimeText));
                }
            }
        }

        private DateTime? _deadlineStart;
        public DateTime? DeadlineStart
        {
            get => _deadlineStart;
            set
            {
                if (SetProperty(ref _deadlineStart, value))
                {
                    RaisePropertyChanged(nameof(HasStarted));
                    RaisePropertyChanged(nameof(IsPending));
                    RaisePropertyChanged(nameof(IsInProgress));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CompletionTimeText));
                }
            }
        }

        private DateTime? _finalizedAt;
        public DateTime? FinalizedAt
        {
            get => _finalizedAt;
            set
            {
                if (SetProperty(ref _finalizedAt, value))
                {
                    RaisePropertyChanged(nameof(IsFinalizedDeadline));
                    RaisePropertyChanged(nameof(IsPending));
                    RaisePropertyChanged(nameof(IsInProgress));
                    RaisePropertyChanged(nameof(IsCompleted));
                    RaisePropertyChanged(nameof(IsFailed));
                    RaisePropertyChanged(nameof(IsEditable));
                    RaisePropertyChanged(nameof(IsInert));
                    RaisePropertyChanged(nameof(ShouldUseFrozenCurrentValue));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(Progress));
                    RaisePropertyChanged(nameof(CurrentValueText));
                    RaisePropertyChanged(nameof(CompletionTimeText));
                }
            }
        }

        private double? _frozenCurrentValue;
        public double? FrozenCurrentValue
        {
            get => _frozenCurrentValue;
            set
            {
                if (SetProperty(ref _frozenCurrentValue, value))
                {
                    RaisePropertyChanged(nameof(ShouldUseFrozenCurrentValue));
                    RaisePropertyChanged(nameof(CurrentValueText));
                    RaisePropertyChanged(nameof(Progress));
                    RaisePropertyChanged(nameof(ActiveTimeText));
                }
            }
        }

        public bool IsDeadlineAchievement => CompletionType == AchievementCompletionType.Deadline;

        public bool HasStarted
        {
            get
            {
                if (!IsDeadlineAchievement)
                    return true;

                var now = ActivityTimeMath.LocalNow;
                return GetDeadlineWindowStart() <= now;
            }
        }

        public bool HasEnded
        {
            get
            {
                if (!IsDeadlineAchievement)
                    return false;

                if (!Deadline.HasValue)
                    return false;

                return ActivityTimeMath.LocalNow > Deadline.Value;
            }
        }

        public bool IsFinalizedDeadline
        {
            get
            {
                return IsDeadlineAchievement && FinalizedAt.HasValue;
            }
        }

        public bool IsPending
        {
            get
            {
                if (!IsDeadlineAchievement || IsFinalizedDeadline)
                    return false;

                var now = ActivityTimeMath.LocalNow;
                return GetDeadlineWindowStart() > now;
            }
        }

        public bool IsCompleted
        {
            get
            {
                if (!IsDeadlineAchievement)
                    return string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);

                return IsFinalizedDeadline &&
                       string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsFailed
        {
            get
            {
                if (!IsDeadlineAchievement)
                    return string.Equals(Status, "Failed", StringComparison.OrdinalIgnoreCase);

                return IsFinalizedDeadline &&
                       string.Equals(Status, "Failed", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsInProgress
        {
            get
            {
                if (!IsDeadlineAchievement || IsFinalizedDeadline)
                    return false;

                var now = ActivityTimeMath.LocalNow;
                var start = GetDeadlineWindowStart();

                if (!Deadline.HasValue)
                    return false;

                return start <= now && now <= Deadline.Value;
            }
        }

        public bool IsEditable
        {
            get
            {
                if (IsDeadlineAchievement && IsFinalizedDeadline)
                    return false;

                return true;
            }
        }

        public bool IsInert
        {
            get
            {
                if (IsDeadlineAchievement && IsFinalizedDeadline)
                    return true;

                return false;
            }
        }

        public bool ShouldUseFrozenCurrentValue
        {
            get
            {
                return IsDeadlineAchievement && IsFinalizedDeadline && FrozenCurrentValue.HasValue;
            }
        }

        public DateTime GetDeadlineWindowStart()
        {
            return DeadlineStart ?? CreatedDate;
        }

        public DateTime GetDeadlineWindowEnd(DateTime now)
        {
            if (!Deadline.HasValue)
                return now;

            return now <= Deadline.Value ? now : Deadline.Value;
        }

        public bool TryGetEvaluationWindow(DateTime now, out DateTime start, out DateTime end)
        {
            if (CompletionType == AchievementCompletionType.Deadline)
            {
                start = GetDeadlineWindowStart();
                end = GetDeadlineWindowEnd(now);

                // invalid if start is in the future relative to the effective end
                if (start > end)
                    return false;

                // invalid if deadline exists and start is after it
                if (Deadline.HasValue && start > Deadline.Value)
                    return false;

                return true;
            }

            start = GetRangeWindowStart(now);
            end = now;

            if (start > end)
                return false;

            return true;
        }

        public string SecondaryDescriptorText
        {
            get
            {
                return TargetType switch
                {
                    AchievementTargetType.Value => string.IsNullOrWhiteSpace(Tags) ? "" : $"Tag: {Tags}",
                    AchievementTargetType.ActiveTime => string.IsNullOrWhiteSpace(Tags) ? "" : $"Tag: {Tags}",
                    AchievementTargetType.Steps => string.IsNullOrWhiteSpace(StepName) ? "" : $"Step: {StepName}",
                    AchievementTargetType.Achievements => string.IsNullOrWhiteSpace(AchievementTitle) ? "" : $"Achievement: {AchievementTitle}",
                    AchievementTargetType.Custom => "Report: " + (string.IsNullOrWhiteSpace("Report") ? "(none)" : "Report"),
                    _ => string.IsNullOrWhiteSpace(Tags) ? "" : $"Tag: {Tags}"
                };
            }
        }

        public double CardOpacity => IsInert ? 0.85 : 1.0;

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

                var now = ActivityTimeMath.LocalNow;
                var windowStart = GetRangeWindowStart(now);
                var lastEarnedAtLocal = ToLocalWallClock(LastEarnedAt.Value);

                // If it was earned within the window, it is locked.
                return lastEarnedAtLocal >= windowStart && lastEarnedAtLocal <= now;
            }
        }

        public DateTime GetRangeWindowStart(DateTime now)
        {
            var amt = RangeAmount;

            var rangeStart = RangeUnit switch
            {
                AchievementRangeUnit.Minutes => now.AddMinutes(-amt),
                AchievementRangeUnit.Hours => now.AddHours(-amt),
                AchievementRangeUnit.Days => now.AddDays(-amt),
                AchievementRangeUnit.Weeks => now.AddDays(-(7 * amt)),
                AchievementRangeUnit.Months => now.AddMonths(-amt),
                _ => now.AddDays(-amt)
            };

            if (LastEarnedAt.HasValue)
            {
                var lastEarnedAtLocal = ToLocalWallClock(LastEarnedAt.Value);
                if (rangeStart < lastEarnedAtLocal)
                    return lastEarnedAtLocal;
            }

            return rangeStart;
        }

        public string GetAvailableIn(DateTime lastEarnedAt)
        {
            lastEarnedAt = ToLocalWallClock(lastEarnedAt);

            // RangeAmount is int, RangeUnit is enum (you already have these).
            var amt = RangeAmount;

            return RangeUnit switch
            {
                AchievementRangeUnit.Minutes => $"Available in {Math.Round((lastEarnedAt.AddMinutes(amt) - ActivityTimeMath.LocalNow).TotalMinutes,2)} mins",
                AchievementRangeUnit.Hours => $"Available in {Math.Round((lastEarnedAt.AddHours(amt) - ActivityTimeMath.LocalNow).TotalHours, 2)} hrs",
                AchievementRangeUnit.Days => $"Available in {Math.Round((lastEarnedAt.AddDays(amt) - ActivityTimeMath.LocalNow).TotalDays, 2)} days",
                AchievementRangeUnit.Weeks => $"Available in {Math.Round((lastEarnedAt.AddDays(7 * amt) - ActivityTimeMath.LocalNow).TotalDays, 2)} days",
                AchievementRangeUnit.Months => $"Available in {Math.Round((lastEarnedAt.AddMonths(amt) - ActivityTimeMath.LocalNow).TotalDays, 2)} days",
                _ => $"Available in {Math.Round((lastEarnedAt.AddDays(amt) - ActivityTimeMath.LocalNow).TotalDays, 2)} days"
            };
        }

        public string StatusDisplay
        {
            get
            {
                if (IsDeadlineAchievement)
                {
                    if (IsCompleted)
                        return "Completed";

                    if (IsFailed)
                        return "Failed";

                    if (IsPending)
                        return "Pending";

                    if (IsInProgress)
                        return "In-Progress";

                    // Fallback for malformed/in-between states
                    return Status;
                }

                if (IsLockedThisRange)
                {
                    if (LastEarnedAt.HasValue)
                    {
                        return $"Locked: {GetAvailableIn(LastEarnedAt.Value)}";
                    }

                    return "Locked";
                }

                return Status;
            }
        }

        public Color CardBackgroundColor => Colors.Black; // IsLockedThisRange ? Color.FromArgb("#2A2A2A") : Colors.Black;

        public Color CardBadgeBackColor => GetBackColorBasedOnDifficulty(); // IsLockedThisRange ? Color.FromArgb("#2A2A2A") : GetBackColorBasedOnDifficulty();
        public Color CardBadgeForeColor => GetForeColorBasedOnDifficulty(); // IsLockedThisRange ? Colors.Gray : GetForeColorBasedOnDifficulty();

        public Color CardForeColor => Colors.White; // IsLockedThisRange ? Colors.Gray : Colors.White;

        private Color GetBackColorBasedOnDifficulty()
        {
            return Difficulty switch
            {
                AchievementDifficultyLevels.Medium => Colors.LightGreen,
                AchievementDifficultyLevels.Hard => Colors.Brown,
                AchievementDifficultyLevels.Ridiculous => Colors.Black,
                AchievementDifficultyLevels.Special => Colors.DarkBlue,
                _ => Colors.White
            };
        }

        private Color GetForeColorBasedOnDifficulty()
        {
            return Difficulty switch
            {
                AchievementDifficultyLevels.Hard or
                AchievementDifficultyLevels.Ridiculous or
                AchievementDifficultyLevels.Special => Colors.White,
                _ => Colors.Black
            };
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
                    RaisePropertyChanged(nameof(IsDeadlineAchievement));
                    RaisePropertyChanged(nameof(IsLockedThisRange));
                    RaisePropertyChanged(nameof(IsFinalizedDeadline));
                    RaisePropertyChanged(nameof(IsPending));
                    RaisePropertyChanged(nameof(IsInProgress));
                    RaisePropertyChanged(nameof(IsCompleted));
                    RaisePropertyChanged(nameof(IsFailed));
                    RaisePropertyChanged(nameof(IsEditable));
                    RaisePropertyChanged(nameof(IsInert));
                    RaisePropertyChanged(nameof(ShouldUseFrozenCurrentValue));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CardBackgroundColor));
                    RaisePropertyChanged(nameof(CompletionTimeText));
                    RaisePropertyChanged(nameof(Progress));
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

        private DateTime? _deadline;
        public DateTime? Deadline
        {
            get => _deadline;
            set
            {
                if (SetProperty(ref _deadline, value))
                {
                    RaisePropertyChanged(nameof(HasEnded));
                    RaisePropertyChanged(nameof(IsInProgress));
                    RaisePropertyChanged(nameof(StatusDisplay));
                    RaisePropertyChanged(nameof(CompletionTimeText));
                }
            }
        }

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
                    RaisePropertyChanged(nameof(EffectiveCurrentValue));
                    RaisePropertyChanged(nameof(Progress));
                    RaisePropertyChanged(nameof(CurrentValueText));
                    RaisePropertyChanged(nameof(ActiveTimeText));
                    RaisePropertyChanged(nameof(TargetText));
                    RaisePropertyChanged(nameof(CompletionTimeText));
                }
            }
        }

        public double EffectiveCurrentValue
        {
            get
            {
                if (ShouldUseFrozenCurrentValue)
                    return FrozenCurrentValue ?? 0;

                return CurrentValue;
            }
        }

        // 0..1
        public double Progress
        {
            get
            {
                double target = TargetType switch
                {
                    AchievementTargetType.ActiveTime => GetTargetSecondsSpent(),
                    AchievementTargetType.Value => TargetValue,
                    AchievementTargetType.Steps => TargetValue,
                    AchievementTargetType.Achievements => TargetValue,
                    AchievementTargetType.Custom => TargetValue,
                    _ => TargetValue
                };

                if (target <= 0) return 0;

                var p = EffectiveCurrentValue / target;

                if (p < 0) return 0;
                if (p > 1) return 1;
                return p;
            }
        }

        public double LockProgress
        {
            get
            {
                if(!IsLockedThisRange) return 0;
                if (LastEarnedAt is null) return 0;

                var now = ActivityTimeMath.LocalNow;
                var lastEarnedAtLocal = ToLocalWallClock(LastEarnedAt.Value);

                var amt = RangeAmount;

                var rangeEnd = RangeUnit switch
                {
                    AchievementRangeUnit.Minutes => lastEarnedAtLocal.AddMinutes(amt),
                    AchievementRangeUnit.Hours => lastEarnedAtLocal.AddHours(amt),
                    AchievementRangeUnit.Days => lastEarnedAtLocal.AddDays(amt),
                    AchievementRangeUnit.Weeks => lastEarnedAtLocal.AddDays((7 * amt)),
                    AchievementRangeUnit.Months => lastEarnedAtLocal.AddMonths(amt),
                    _ => now.AddDays(amt)
                };

                //"Now" as a position between LastEarnedAt and rangeEnd gives us lock progress.
                var totalLockTime = (rangeEnd - lastEarnedAtLocal).TotalSeconds;
                if (totalLockTime <= 0) return 1;
                var remainingLockTime = (rangeEnd - now).TotalSeconds;
                var lockProgress = 1 - (remainingLockTime / totalLockTime);
                if (lockProgress < 0) return 0;
                if (lockProgress > 1) return 1;
                return lockProgress;
            }
        }

        // Labels the card needs
        public string ActiveTimeText
        {
            get
            {
                if (TargetType != AchievementTargetType.ActiveTime)
                    return "Active: --:--:--";

                var hours = EffectiveCurrentValue / 3600.0;
                return $"Current Hrs: {hours:0.##}";
            }
        }

        public bool IsActiveTimeTextVisible => TargetType == AchievementTargetType.ActiveTime;

        public string TargetTypeText => TargetType switch
        {
            AchievementTargetType.ActiveTime => "Target: Active Time",
            AchievementTargetType.Value => "Target: Value",
            AchievementTargetType.Steps => "Target: Steps",
            _ => "Target: ?"
        };

        public string CurrentValueText
        {
            get
            {
                var v = EffectiveCurrentValue.ToString("0.##", CultureInfo.InvariantCulture);

                return TargetType switch
                {
                    AchievementTargetType.Value => $"Current Pts: {v}",
                    AchievementTargetType.Steps => $"Current Reps: {v}",
                    AchievementTargetType.Achievements => $"Current: {v}",
                    AchievementTargetType.Custom => $"Current: {v}",
                    _ => $"Current: {v}"
                };
            }
        }

        public bool IsCurrentValueTextVisible => TargetType == AchievementTargetType.Value;

        public string TargetText
        {
            get
            {
                var v = TargetValue.ToString("0.##", CultureInfo.InvariantCulture);

                return TargetType switch
                {
                    AchievementTargetType.ActiveTime => $"Target Hrs: {ActiveTimeTargetText}",
                    AchievementTargetType.Value => $"Target Pts: {v}",
                    AchievementTargetType.Steps => $"Target Reps: {v}",
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
                    if (Deadline is null)
                        return "Completion: by (no deadline)";

                    var start = GetDeadlineWindowStart();
                    var now = ActivityTimeMath.LocalNow;
                    var remaining = Deadline.Value - now;

                    string remainingText = remaining.TotalSeconds >= 0
                        ? $"in {Math.Round(remaining.TotalDays, 2)} days"
                        : $"{Math.Round(Math.Abs(remaining.TotalDays), 2)} days ago";

                    return $"Completion: {start:yyyy-MM-dd} to {Deadline.Value:yyyy-MM-dd} [{remainingText}]";
                }

                return $"Completion: Over the last {RangeAmount} {RangeUnit} [{GetRangeWindowStart(ActivityTimeMath.LocalNow):MMM-dd}]";
            }
        }

        public int Target { get; internal set; }
        public DateTime CompletedAt { get; internal set; }


        private ObservableCollection<string> _trophies = new();
        public ObservableCollection<string> Trophies
        {
            get => _trophies;
            set
            {
                if (ReferenceEquals(_trophies, value))
                    return;

                if (_trophies != null)
                    _trophies.CollectionChanged -= OnTrophiesCollectionChanged;

                _trophies = value ?? new ObservableCollection<string>();

                _trophies.CollectionChanged += OnTrophiesCollectionChanged;

                RaisePropertyChanged(nameof(Trophies));
                RaisePropertyChanged(nameof(TrophyCount));
            }
        }

        public int TrophyCount => Trophies.Count;

        private void OnTrophiesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(TrophyCount));
        }

        public int Id { get; set; }


        // For now: Achievements don’t contribute to global value until you define how they pay out.
        public double GetValue(DateTime start, DateTime end) => 0;

        // Call this when something time-based changes (later)
        public void NotifyTimeChanged()
        {
            RaisePropertyChanged(nameof(HasStarted));
            RaisePropertyChanged(nameof(HasEnded));
            RaisePropertyChanged(nameof(IsPending));
            RaisePropertyChanged(nameof(IsInProgress));
            RaisePropertyChanged(nameof(IsCompleted));
            RaisePropertyChanged(nameof(IsFailed));
            RaisePropertyChanged(nameof(IsEditable));
            RaisePropertyChanged(nameof(IsInert));
            RaisePropertyChanged(nameof(ShouldUseFrozenCurrentValue));
            RaisePropertyChanged(nameof(EffectiveCurrentValue));
            RaisePropertyChanged(nameof(ActiveTimeText));
            RaisePropertyChanged(nameof(TargetTypeText));
            RaisePropertyChanged(nameof(CurrentValueText));
            RaisePropertyChanged(nameof(TargetText));
            RaisePropertyChanged(nameof(CompletionTimeText));
            RaisePropertyChanged(nameof(StatusDisplay));
            RaisePropertyChanged(nameof(Progress));
            RaisePropertyChanged(nameof(LockProgress));
        }

        public double GetTargetSecondsSpent()
        {
             double retval = 0;

            if(!string.IsNullOrEmpty(_activeTimeTargetText))
            {
                string hrsString = _activeTimeTargetText.Split(':')[0];
                string minsString = _activeTimeTargetText.Split(':')[1];
                string secsString = _activeTimeTargetText.Split(':')[2];

                if (!int.TryParse(hrsString, out int hrs)) throw new Exception("Hours could not be parsed!");

                if (!int.TryParse(minsString, out int mins)) throw new Exception("Mins could not be parsed!");

                if (!int.TryParse(secsString, out int secs)) throw new Exception("Secs could not be parsed!");

                retval += hrs * 3600;

                retval += mins * 60;

                retval += secs;

            }

            return retval;
        }

        public void UpdatePerTick(IEnumerable<Evaluators.TimeValueAchievementEvaluation> evaluations)
        {
            // Finalized deadline achievements are inert/frozen.
            if (IsFinalizedDeadline)
            {
                if (FrozenCurrentValue.HasValue)
                    CurrentValue = FrozenCurrentValue.Value;

                RaisePropertyChanged(nameof(EffectiveCurrentValue));
                RaisePropertyChanged(nameof(CurrentValueText));
                RaisePropertyChanged(nameof(ActiveTimeText));
                RaisePropertyChanged(nameof(CompletionTimeText));
                RaisePropertyChanged(nameof(StatusDisplay));
                RaisePropertyChanged(nameof(LockProgress));
                RaisePropertyChanged(nameof(Progress));
                return;
            }

            var relevantEvaluations = evaluations?
                .Where(x => x?.AchievementCard != null && x.AchievementCard.Id == Id)
                .ToList()
                ?? new List<Evaluators.TimeValueAchievementEvaluation>();

            switch (TargetType)
            {
                case AchievementTargetType.ActiveTime:
                    CurrentValue = relevantEvaluations.Sum(x => x.CurrentValue);
                    break;

                case AchievementTargetType.Value:
                    CurrentValue = relevantEvaluations.Sum(x => x.CurrentValue);
                    break;

                case AchievementTargetType.Steps:
                    break;

                case AchievementTargetType.Achievements:
                    break;

                case AchievementTargetType.Custom:
                    break;

                default:
                    break;
            }

            RaisePropertyChanged(nameof(EffectiveCurrentValue));
            RaisePropertyChanged(nameof(ActiveTimeText));
            RaisePropertyChanged(nameof(CurrentValueText));
            RaisePropertyChanged(nameof(CompletionTimeText));
            RaisePropertyChanged(nameof(StatusDisplay));
            RaisePropertyChanged(nameof(LockProgress));
            RaisePropertyChanged(nameof(Progress));
        }

        public bool ShouldCompleteNow(double currentValue, DateTime now)
        {
            if (!IsDeadlineAchievement || IsFinalizedDeadline)
                return false;

            if (!TryGetEvaluationWindow(now, out var start, out var end))
                return false;

            if (start > now)
                return false;

            return TargetValue > 0 && currentValue >= TargetValue;
        }

        public bool ShouldFailNow(double currentValue, DateTime now)
        {
            if (!IsDeadlineAchievement || IsFinalizedDeadline)
                return false;

            if (!Deadline.HasValue)
                return false;

            if (TargetValue > 0 && currentValue >= TargetValue)
                return false;

            return now > Deadline.Value;
        }

        public bool ShouldStillBeShownToday(DateTime now)
        {
            if (!IsDeadlineAchievement)
                return true;

            if (!FinalizedAt.HasValue)
                return true;

            var todayStart = now.Date;
            var tomorrowStart = todayStart.AddDays(1);

            var finalizedAtLocal = ToLocalWallClock(FinalizedAt.Value);
            return finalizedAtLocal >= todayStart && finalizedAtLocal < tomorrowStart;
        }

        private static DateTime ToLocalWallClock(DateTime value) => value.Kind == DateTimeKind.Utc
            ? value.ToLocalTime()
            : value;
    }
}
