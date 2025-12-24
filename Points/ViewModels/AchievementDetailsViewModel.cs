using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class AchievementDetailsViewModel : ObservableObject
    {
        private readonly AchievementCardModel _model;
        public AchievementCardModel Model => _model;

        private readonly Action<AchievementCardModel> _onSaved;

        public AchievementDetailsViewModel(AchievementCardModel model, Action<AchievementCardModel> onSaved)
        {
            _model = model;
            _onSaved = onSaved;

            SaveCommand = new Command(async () => await SaveAsync());

            // load editable fields from model
            Title = _model.Title;
            Tags = _model.Tags;

            GoalType = _model.GoalType;

            TargetValueText = _model.TargetValue.ToString("0.##", CultureInfo.InvariantCulture);
            StepName = _model.StepName;
            AchievementTitle = _model.AchievementTitle;

            CompletionType = _model.CompletionType;

            RangeUnit = _model.RangeUnit;
            RangeAmountText = _model.RangeAmount.ToString(CultureInfo.InvariantCulture);

            DeadlineDate = (_model.Deadline ?? DateTime.MinValue).Date;
            DeadlineTime = (_model.Deadline ?? DateTime.MinValue).TimeOfDay;
        }

        private TimeSpan? _activeTimeTarget;
        public TimeSpan? ActiveTimeTarget
        {
            get => _activeTimeTarget;
            set => SetProperty(ref _activeTimeTarget, value);
        }

        private string _activeTimeTargetText = "";
        public string ActiveTimeTargetText
        {
            get => _activeTimeTargetText;
            set => SetProperty(ref _activeTimeTargetText, value);
        }


        // Editable
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        public string Status => _model.Status; // read-only

        private AchievementGoalType _goalType;
        public AchievementGoalType GoalType
        {
            get => _goalType;
            set
            {
                if (!SetProperty(ref _goalType, value)) return;

                // Tell the UI that all dependent visibility properties changed
                RaisePropertyChanged(nameof(IsActiveTimeTargetVisible));
                RaisePropertyChanged(nameof(IsValueTargetVisible));
                RaisePropertyChanged(nameof(IsStepTargetVisible));
                RaisePropertyChanged(nameof(IsAchievementTargetVisible));
            }
        }

        private string _targetValueText = "0";
        public string TargetValueText { get => _targetValueText; set => SetProperty(ref _targetValueText, value); }

        private string _stepName = "";
        public string StepName { get => _stepName; set => SetProperty(ref _stepName, value); }

        private string _achievementTitle = "";
        public string AchievementTitle { get => _achievementTitle; set => SetProperty(ref _achievementTitle, value); }

        private AchievementCompletionType _completionType;
        public AchievementCompletionType CompletionType
        {
            get => _completionType;
            set
            {
                if (!SetProperty(ref _completionType, value)) return;
                RaisePropertyChanged(nameof(IsRangeVisible));
                RaisePropertyChanged(nameof(IsDeadlineVisible));
            }
        }

        private AchievementRangeUnit _rangeUnit = AchievementRangeUnit.Days;
        public AchievementRangeUnit RangeUnit { get => _rangeUnit; set => SetProperty(ref _rangeUnit, value); }

        private string _rangeAmountText = "7";
        public string RangeAmountText { get => _rangeAmountText; set => SetProperty(ref _rangeAmountText, value); }

        private DateTime _deadlineDate = DateTime.Now.Date;
        public DateTime DeadlineDate { get => _deadlineDate; set => SetProperty(ref _deadlineDate, value); }

        private TimeSpan _deadlineTime = DateTime.Now.TimeOfDay;
        public TimeSpan DeadlineTime { get => _deadlineTime; set => SetProperty(ref _deadlineTime, value); }


        // ===== Goal-type-specific target visibility =====

        public bool IsActiveTimeTargetVisible => GoalType == AchievementGoalType.ActiveTime;

        public bool IsValueTargetVisible => GoalType == AchievementGoalType.Value;

        public bool IsStepTargetVisible => GoalType == AchievementGoalType.Steps;

        public bool IsAchievementTargetVisible => GoalType == AchievementGoalType.Achievements;


        // ===== Completion-type visibility =====

        public bool IsRangeVisible => CompletionType == AchievementCompletionType.Range;

        public bool IsDeadlineVisible => CompletionType == AchievementCompletionType.Deadline;


        // ===== Picker data =====

        public IReadOnlyList<AchievementGoalType> GoalTypeOptions { get; }
            = Enum.GetValues(typeof(AchievementGoalType))
                  .Cast<AchievementGoalType>()
                  .ToList();


        //private TimeSpan _activeTimeTarget = TimeSpan.Zero;

        //public string ActiveTimeText
        //{
        //    get => $"{(int)_activeTimeTarget.TotalHours:00}:{_activeTimeTarget.Minutes:00}:{_activeTimeTarget.Seconds:00}";
        //    set
        //    {
        //        if (TimeSpan.TryParse(value, out var ts))
        //        {
        //            _activeTimeTarget = ts;
        //            RaisePropertyChanged();
        //        }
        //    }
        //}


        public Command SaveCommand { get; }

        private async Task SaveAsync()
        {
            // Parse numeric target
            if (!double.TryParse(TargetValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetVal))
                targetVal = 0;

            // Parse range amount
            if (!int.TryParse(RangeAmountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rangeAmt))
                rangeAmt = 0;

            // Compose deadline
            var deadline = DeadlineDate.Date + DeadlineTime;

            // Commit
            _model.Title = Title;
            _model.Tags = Tags;

            _model.GoalType = GoalType;
            _model.ActiveTimeTargetText = ActiveTimeTargetText;
            _model.TargetValue = targetVal;
            _model.StepName = StepName;
            _model.AchievementTitle = AchievementTitle;

            _model.CompletionType = CompletionType;
            _model.RangeUnit = RangeUnit;
            _model.RangeAmount = rangeAmt;
            _model.Deadline = deadline;

            _onSaved(_model);

            await Shell.Current.Navigation.PopAsync();
        }
    }
}
