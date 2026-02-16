using Points.Global;
using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class AchievementDetailsViewModel : ObservableObject
    {
        private readonly AchievementCardModel _model;
        public AchievementCardModel Model => _model;

        private readonly Action<AchievementCardModel> _onSaved;
        private readonly Action<AchievementCardModel> _onDelete;

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        public Command CancelCommand { get; }

        public Command ViewTrophiesCommand { get; }

        public List<string> TrophiesToAdd { get; set; } = new List<string>();

        public AchievementDetailsViewModel(AchievementCardModel model, Action<AchievementCardModel> onSaved, Action<AchievementCardModel> onDelete)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete;

            _model.Trophies = GetAchievementTrophies();

            CancelCommand = new Command(async () => await OnCancelAsync());
            ViewTrophiesCommand = new Command(async () => await OnViewTrophiesAsync());

            // Tick every second
            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, __) =>
            {

            };
            _timer.Start();

            SaveCommand = new Command(async () => await SaveAsync());

            // load editable fields from model
            Title = _model.Title;
            IsPinned = _model.IsPinned;
            Tags = _model.Tags;

            GoalType = _model.GoalType;
            DifficultyLevel = _model.Difficulty;

            TargetValueText = _model.TargetValue.ToString("0.##", CultureInfo.InvariantCulture);
            ActiveTimeTargetText = _model.ActiveTimeTargetText;

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
                RaisePropertyChanged(nameof(IsCustomReportTargetVisible));
            }
        }

        private AchievementDifficultyLevels _difficultyLevel;
        public AchievementDifficultyLevels DifficultyLevel
        {
            get => _difficultyLevel;
            set
            {
                if (!SetProperty(ref _difficultyLevel, value)) return;

                // Tell the UI that all dependent visibility properties changed
                RaisePropertyChanged(nameof(DifficultyLevel));
            }
        }

        private string _targetValueText = "0";
        public string TargetValueText { get => _targetValueText; set => SetProperty(ref _targetValueText, value); }

        private string _stepName = "";
        public string StepName { get => _stepName; set => SetProperty(ref _stepName, value); }

        private string _reportName = "";
        public string ReportName { get => _reportName; set => SetProperty(ref _reportName, value); }

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

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (!SetProperty(ref _isPinned, value)) return;
                RaisePropertyChanged(nameof(IsPinned));
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

        public bool IsCustomReportTargetVisible => GoalType == AchievementGoalType.Custom;


        // ===== Completion-type visibility =====

        public bool IsRangeVisible => CompletionType == AchievementCompletionType.Range;

        public bool IsDeadlineVisible => CompletionType == AchievementCompletionType.Deadline;


        // ===== Picker data =====

        public IReadOnlyList<AchievementGoalType> GoalTypeOptions { get; }
            = Enum.GetValues(typeof(AchievementGoalType))
                  .Cast<AchievementGoalType>()
                  .ToList();

        private async Task OnCancelAsync()
        {
            var choice = await Shell.Current.DisplayActionSheet(
                _model.Title,
                "Cancel",
                null,
                "Delete"
            );

            if (choice == "Delete")
            {
                _onDelete?.Invoke(_model);
                await Shell.Current.Navigation.PopAsync();
            }
        }

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
            _model.IsPinned = IsPinned;
            _model.Tags = Tags;

            _model.GoalType = GoalType;
            _model.Difficulty = DifficultyLevel;
            _model.ActiveTimeTargetText = ActiveTimeTargetText;
            _model.TargetValue = targetVal;
            _model.StepName = StepName;
            _model.AchievementTitle = AchievementTitle;

            _model.CompletionType = CompletionType;
            _model.RangeUnit = RangeUnit;
            _model.RangeAmount = rangeAmt;
            _model.Deadline = deadline;

            SaveTrophiesToDisk();

            _onSaved(_model);

            await Shell.Current.Navigation.PopAsync();
        }

        private void SaveTrophiesToDisk()
        {
            string targetTrophyFolderPath = AppPaths.GetAchievementTrophiesPath(_model.Id);

            foreach (var trophyToAdd in TrophiesToAdd.Where(x => !string.IsNullOrEmpty(x)))
            {
                var fileContent = File.ReadAllBytes(trophyToAdd);

                if(fileContent != null)
                {
                    var trohpyFileName = Path.GetFileName(trophyToAdd);

                    var trophyFullTargetPath = Path.Combine(targetTrophyFolderPath, trohpyFileName);

                    // Overwrite if it already exists
                    File.WriteAllBytes(trophyFullTargetPath, fileContent);
                }
            }
        }

        private ObservableCollection<string> GetAchievementTrophies()
        {
            string targetTrophyFolderPath = AppPaths.GetAchievementTrophiesPath(_model.Id);

            if (!Directory.Exists(targetTrophyFolderPath)) return new ObservableCollection<string>();

            var trophies = Directory
                            .EnumerateFiles(targetTrophyFolderPath)
                            .Select(Path.GetFileName)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

            return new ObservableCollection<string>(trophies);
        }

        private async Task OnViewTrophiesAsync()
        {
            var trophies = _model.Trophies?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList() ?? new List<string>();

            if (trophies.Count == 0)
            {
                await Application.Current.MainPage.DisplayAlert("Trophies", "No trophies saved.", "OK");
                return;
            }

            // ActionSheet supports a cancel + optional destruction button, and a list of options.
            // Note: ActionSheet is best for up to ~10-15 items; beyond that, use a modal page.
            var selected = await Application.Current.MainPage.DisplayActionSheet(
                "Trophies",
                "Close",
                null,
                trophies.ToArray());

            // Optional: If you want to do something when one is picked (copy/open/etc)
            // if (!string.IsNullOrWhiteSpace(selected) && selected != "Close")
            // {
            //     await Application.Current.MainPage.DisplayAlert("Selected trophy", selected, "OK");
            // }
        }
    }
}
