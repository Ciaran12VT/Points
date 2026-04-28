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

        public IReadOnlyList<AchievementDifficultyLevels> DifficultyLevelOptions { get; } = Enum.GetValues(typeof(AchievementDifficultyLevels)).Cast<AchievementDifficultyLevels>().ToList();

        public IReadOnlyList<AchievementCompletionType> CompletionTypeOptions { get; } = Enum.GetValues(typeof(AchievementCompletionType)).Cast<AchievementCompletionType>().ToList();

        public IReadOnlyList<AchievementRangeUnit> RangeUnitOptions { get; } = Enum.GetValues(typeof(AchievementRangeUnit)).Cast<AchievementRangeUnit>().ToList();

        private readonly AchievementCardModel _model;
        public AchievementCardModel Model => _model;

        private readonly Func<AchievementCardModel, Task> _onSaved;
        private readonly Action<AchievementCardModel> _onDelete;

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        public Command CancelCommand { get; }

        public Command ViewTrophiesCommand { get; }

        private ObservableCollection<string> _trophiesToAdd = new();
        public ObservableCollection<string> TrophiesToAdd
        {
            get => _trophiesToAdd;
            set
            {
                if (_trophiesToAdd != null)
                    _trophiesToAdd.CollectionChanged -= OnTrophiesToAddChanged;

                if (SetProperty(ref _trophiesToAdd, value ?? new ObservableCollection<string>()))
                {
                    _trophiesToAdd.CollectionChanged += OnTrophiesToAddChanged;
                    RaisePropertyChanged(nameof(PendingTrophyCount));
                    RaisePropertyChanged(nameof(HasPendingTrophies));
                    RaisePropertyChanged(nameof(PendingTrophyCountText));
                }
            }
        }
        private void OnTrophiesToAddChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(PendingTrophyCount));
            RaisePropertyChanged(nameof(HasPendingTrophies));
            RaisePropertyChanged(nameof(PendingTrophyCountText));
        }

        public bool HasPendingTrophies => PendingTrophyCount > 0;

        public int PendingTrophyCount
        {
            get
            {
                var existing = new HashSet<string>(
                    Trophies.Where(x => !string.IsNullOrWhiteSpace(x)),
                    StringComparer.OrdinalIgnoreCase);

                return TrophiesToAdd
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(Path.GetFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(fileName => !existing.Contains(fileName!));
            }
        }

        public string PendingTrophyCountText => $"+{PendingTrophyCount}";

        private ObservableCollection<string> _trophies = new();
        public ObservableCollection<string> Trophies
        {
            get => _trophies;
            set
            {
                if (_trophies != null)
                    _trophies.CollectionChanged -= OnVmTrophiesCollectionChanged;

                if (SetProperty(ref _trophies, value ?? new ObservableCollection<string>()))
                {
                    _trophies.CollectionChanged += OnVmTrophiesCollectionChanged;
                    RaisePropertyChanged(nameof(TrophyCount));
                }
            }
        }

        public int TrophyCount => Trophies.Count;

        private void OnVmTrophiesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(TrophyCount));
        }

        public bool IsReadOnly => _model.IsDeadlineAchievement && _model.IsFinalizedDeadline;

        public bool CanEdit => !IsReadOnly;

        public bool CanSave => !IsReadOnly && string.IsNullOrWhiteSpace(ValidationMessage);

        private string _validationMessage = "";
        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (SetProperty(ref _validationMessage, value))
                {
                    RaisePropertyChanged(nameof(HasValidationMessage));
                    RaisePropertyChanged(nameof(CanSave));
                }
            }
        }

        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

        public bool CanDelete => !IsReadOnly;

        public bool IsFinalizedDeadline => _model.IsDeadlineAchievement && _model.IsFinalizedDeadline;

        public bool IsPending => _model.IsPending;

        public bool IsInProgress => _model.IsInProgress;

        public bool IsCompleted => _model.IsCompleted;

        public bool IsFailed => _model.IsFailed;

        public string StatusDisplay => _model.StatusDisplay;

        private DateTime _deadlineStartDate = DateTime.Now.Date;
        public DateTime DeadlineStartDate
        {
            get => _deadlineStartDate;
            set
            {
                if (SetProperty(ref _deadlineStartDate, value))
                {
                    Validate();
                }
            }
        }

        private TimeSpan _deadlineStartTime = DateTime.Now.TimeOfDay;
        public TimeSpan DeadlineStartTime
        {
            get => _deadlineStartTime;
            set
            {
                if (SetProperty(ref _deadlineStartTime, value))
                {
                    Validate();
                }
            }
        }

        public AchievementDetailsViewModel(AchievementCardModel model, Func<AchievementCardModel, Task> onSaved, Action<AchievementCardModel> onDelete)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete;

            Trophies = GetAchievementTrophies();

            _model.Trophies.Clear();
            foreach (var trophy in Trophies)
            {
                _model.Trophies.Add(trophy);
            }

            TrophiesToAdd.CollectionChanged += OnTrophiesToAddChanged;

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

            TargetType = _model.TargetType;
            DifficultyLevel = _model.Difficulty;

            TargetValueText = _model.TargetValue.ToString("0.##", CultureInfo.InvariantCulture);
            ActiveTimeTargetText = _model.ActiveTimeTargetText;

            StepName = _model.StepName;
            AchievementTitle = _model.AchievementTitle;

            CompletionType = _model.CompletionType;

            RangeUnit = _model.RangeUnit;
            RangeAmountText = _model.RangeAmount.ToString(CultureInfo.InvariantCulture);

            var deadlineStart = _model.DeadlineStart ?? _model.CreatedDate;
            DeadlineStartDate = deadlineStart.Date;
            DeadlineStartTime = deadlineStart.TimeOfDay;

            var deadline = _model.Deadline ?? DateTime.Now;
            DeadlineDate = deadline.Date;
            DeadlineTime = deadline.TimeOfDay;

            Validate();

            RaiseEditorStateChanged();
        }

        public string DeadlineWindowSummary
        {
            get
            {
                var start = DeadlineStartDate.Date + DeadlineStartTime;
                var end = DeadlineDate.Date + DeadlineTime;

                return $"Window: {start:yyyy-MM-dd HH:mm} → {end:yyyy-MM-dd HH:mm}";
            }
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
            set
            {
                if (SetProperty(ref _activeTimeTargetText, value))
                {
                    Validate();
                }
            }
        }


        // Editable
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        public string Status => _model.Status; // read-only

        private AchievementTargetType _targetType;
        public AchievementTargetType TargetType
        {
            get => _targetType;
            set
            {
                if (!SetProperty(ref _targetType, value)) return;

                RaisePropertyChanged(nameof(IsActiveTimeTargetVisible));
                RaisePropertyChanged(nameof(IsValueTargetVisible));
                RaisePropertyChanged(nameof(IsStepTargetVisible));
                RaisePropertyChanged(nameof(IsAchievementTargetVisible));
                RaisePropertyChanged(nameof(IsCustomReportTargetVisible));

                Validate();
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
                //RaisePropertyChanged(nameof(DifficultyLevel));
            }
        }

        private string _targetValueText = "0";
        public string TargetValueText
        {
            get => _targetValueText;
            set
            {
                if (SetProperty(ref _targetValueText, value))
                {
                    Validate();
                }
            }
        }

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
                Validate();
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
        public string RangeAmountText
        {
            get => _rangeAmountText;
            set
            {
                if (SetProperty(ref _rangeAmountText, value))
                {
                    Validate();
                }
            }
        }

        private DateTime _deadlineDate = DateTime.Now.Date;
        public DateTime DeadlineDate
        {
            get => _deadlineDate;
            set
            {
                if (SetProperty(ref _deadlineDate, value))
                {
                    Validate();
                }
            }
        }

        private TimeSpan _deadlineTime = DateTime.Now.TimeOfDay;
        public TimeSpan DeadlineTime
        {
            get => _deadlineTime;
            set
            {
                if (SetProperty(ref _deadlineTime, value))
                {
                    Validate();
                }
            }
        }

        public bool IsDeadlineStartVisible => CompletionType == AchievementCompletionType.Deadline;

        public bool IsDeadlineReadOnlyStatusVisible => IsReadOnly && _model.IsDeadlineAchievement;

        public string ReadOnlyMessage
        {
            get
            {
                if (!IsReadOnly) return "";

                if (IsCompleted)
                    return "This achievement has been completed and is now read-only.";

                if (IsFailed)
                    return "This achievement has failed and is now read-only.";

                return "This achievement is read-only.";
            }
        }

        private void Validate()
        {
            if (IsReadOnly)
            {
                ValidationMessage = "";
                return;
            }

            if (CompletionType == AchievementCompletionType.Deadline)
            {
                var deadlineStart = DeadlineStartDate.Date + DeadlineStartTime;
                var deadline = DeadlineDate.Date + DeadlineTime;

                if (deadline == default)
                {
                    ValidationMessage = "A deadline is required.";
                    return;
                }

                if (deadlineStart > deadline)
                {
                    ValidationMessage = "Deadline start cannot be later than the deadline.";
                    return;
                }
            }

            if (CompletionType == AchievementCompletionType.Range)
            {
                if (!int.TryParse(RangeAmountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rangeAmt) || rangeAmt <= 0)
                {
                    ValidationMessage = "Range amount must be a whole number greater than 0.";
                    return;
                }
            }

            if (TargetType == AchievementTargetType.Value ||
                TargetType == AchievementTargetType.Steps ||
                TargetType == AchievementTargetType.Achievements ||
                TargetType == AchievementTargetType.Custom)
            {
                if (!double.TryParse(TargetValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetVal) || targetVal <= 0)
                {
                    ValidationMessage = "Target value must be greater than 0.";
                    return;
                }
            }

            ValidationMessage = "";
        }

        // ===== Target-type-specific target visibility =====

        public bool IsActiveTimeTargetVisible => TargetType == AchievementTargetType.ActiveTime;

        public bool IsValueTargetVisible => TargetType == AchievementTargetType.Value;

        public bool IsStepTargetVisible => TargetType == AchievementTargetType.Steps;

        public bool IsAchievementTargetVisible => TargetType == AchievementTargetType.Achievements;

        public bool IsCustomReportTargetVisible => TargetType == AchievementTargetType.Custom;


        // ===== Completion-type visibility =====

        public bool IsRangeVisible => CompletionType == AchievementCompletionType.Range;

        public bool IsDeadlineVisible => CompletionType == AchievementCompletionType.Deadline;


        // ===== Picker data =====

        public IReadOnlyList<AchievementTargetType> TargetTypeOptions { get; }
            = Enum.GetValues(typeof(AchievementTargetType))
                  .Cast<AchievementTargetType>()
                  .ToList();

        private async Task OnCancelAsync()
        {
            if (IsReadOnly)
            {
                await Shell.Current.Navigation.PopAsync();
                return;
            }

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
            if (IsReadOnly)
                return;

            Validate();

            if (HasValidationMessage)
            {
                await Shell.Current.DisplayAlert("Invalid Achievement", ValidationMessage, "OK");
                return;
            }

            if (!double.TryParse(TargetValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetVal))
                targetVal = 0;

            if (!int.TryParse(RangeAmountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rangeAmt))
                rangeAmt = 0;

            var deadlineStart = DeadlineStartDate.Date + DeadlineStartTime;
            var deadline = DeadlineDate.Date + DeadlineTime;

            _model.Title = Title;
            _model.IsPinned = IsPinned;
            _model.Tags = Tags;

            _model.TargetType = TargetType;
            _model.Difficulty = DifficultyLevel;
            _model.ActiveTimeTargetText = ActiveTimeTargetText;
            _model.TargetValue = targetVal;
            _model.StepName = StepName;
            _model.AchievementTitle = AchievementTitle;

            _model.CompletionType = CompletionType;
            _model.RangeUnit = RangeUnit;
            _model.RangeAmount = rangeAmt;

            if (CompletionType == AchievementCompletionType.Deadline)
            {
                _model.DeadlineStart = deadlineStart;
                _model.Deadline = deadline;
            }
            else
            {
                _model.DeadlineStart = null;
                _model.Deadline = null;
            }

            await _onSaved(_model);
            SaveTrophiesToDisk();

            await Shell.Current.Navigation.PopAsync();
        }

        private void SaveTrophiesToDisk()
        {
            if (_model.Id <= 0) return;

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

                    if (!Trophies.Contains(trohpyFileName))
                        Trophies.Add(trohpyFileName);
                }

                _model.Trophies.Clear();
                foreach (var trophy in Trophies)
                {
                    _model.Trophies.Add(trophy);
                }

                TrophiesToAdd.Clear();
                RaisePropertyChanged(nameof(PendingTrophyCount));
                RaisePropertyChanged(nameof(HasPendingTrophies));
                RaisePropertyChanged(nameof(PendingTrophyCountText));
            }
        }

        private ObservableCollection<string> GetAchievementTrophies()
        {
            if (_model.Id <= 0) return new ObservableCollection<string>();

            string targetTrophyFolderPath = AppPaths.GetAchievementTrophiesPath(_model.Id);

            if (!Directory.Exists(targetTrophyFolderPath)) return new ObservableCollection<string>();

            var trophies = Directory
                            .EnumerateFiles(targetTrophyFolderPath)
                            .Select(Path.GetFileName)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

            return new ObservableCollection<string>(trophies!);
        }

        private async Task OnViewTrophiesAsync()
        {
            var trophies = Trophies?
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

        private void RaiseEditorStateChanged()
        {
            RaisePropertyChanged(nameof(IsReadOnly));
            RaisePropertyChanged(nameof(CanEdit));
            RaisePropertyChanged(nameof(CanSave));
            RaisePropertyChanged(nameof(CanDelete));
            RaisePropertyChanged(nameof(IsFinalizedDeadline));
            RaisePropertyChanged(nameof(IsPending));
            RaisePropertyChanged(nameof(IsInProgress));
            RaisePropertyChanged(nameof(IsCompleted));
            RaisePropertyChanged(nameof(IsFailed));
            RaisePropertyChanged(nameof(StatusDisplay));
            RaisePropertyChanged(nameof(IsDeadlineReadOnlyStatusVisible));
            RaisePropertyChanged(nameof(ReadOnlyMessage));
        }
    }
}
