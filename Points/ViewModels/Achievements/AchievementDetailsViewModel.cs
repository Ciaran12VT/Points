using Points.Global;
using Points.ViewModels.Shared;
using Points.Models;
using Points.Services.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Points.Services.Time;

namespace Points.ViewModels.Achievements
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
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly ActiveCardDetailsInteractionCoordinator _detailsInteractions;
        private readonly IReadOnlyList<string> _achievementTitleOptions;
        private readonly IClock _clock;

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        public Command CancelCommand { get; }

        public Command ViewTrophiesCommand { get; }
        public Command EditTagsCommand { get; }
        public Command ClearTagsCommand { get; }
        public Command EditActiveTimeTargetCommand { get; }
        public Command EditAchievementsCommand { get; }
        public Command ClearAchievementsCommand { get; }
        public Command AddTrophyPhotoCommand { get; }
        public Command AddTrophyFileCommand { get; }
        public Command ClearTrophiesCommand { get; }

        public IReadOnlyList<string> TagOptions { get; }
        public IReadOnlyList<string> StepNameOptions { get; }
        public IReadOnlyList<string> ReportNameOptions { get; }

        private ObservableCollection<string> _trophiesToAdd = new();
        public ObservableCollection<string> TrophiesToAdd
        {
            get => _trophiesToAdd;
            set
            {
                var newValue = value ?? new ObservableCollection<string>();
                if (ReferenceEquals(_trophiesToAdd, newValue))
                    return;

                _trophiesToAdd.CollectionChanged -= OnTrophiesToAddChanged;
                _trophiesToAdd = newValue;
                _trophiesToAdd.CollectionChanged += OnTrophiesToAddChanged;

                RaisePropertyChanged(nameof(TrophiesToAdd));
                RaisePropertyChanged(nameof(PendingTrophyCount));
                RaisePropertyChanged(nameof(HasPendingTrophies));
                RaisePropertyChanged(nameof(PendingTrophyCountText));
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
                var newValue = value ?? new ObservableCollection<string>();
                if (ReferenceEquals(_trophies, newValue))
                    return;

                _trophies.CollectionChanged -= OnVmTrophiesCollectionChanged;
                _trophies = newValue;
                _trophies.CollectionChanged += OnVmTrophiesCollectionChanged;

                RaisePropertyChanged(nameof(Trophies));
                RaisePropertyChanged(nameof(TrophyCount));
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

        private DateTime _deadlineStartDate;
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

        private TimeSpan _deadlineStartTime;
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

        public AchievementDetailsViewModel(
            AchievementCardModel model,
            IEnumerable<string> allTags,
            IEnumerable<string> stepNames,
            IEnumerable<string> achievementTitles,
            Func<AchievementCardModel, Task> onSaved,
            Action<AchievementCardModel> onDelete,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            IClock clock)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete;
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _detailsInteractions = new ActiveCardDetailsInteractionCoordinator(_navigation, _dialogs, clock: _clock);
            TagOptions = allTags?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
            StepNameOptions = stepNames?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
            ReportNameOptions = new List<string> { "Report 1", "Report 2" };
            _achievementTitleOptions = achievementTitles?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();

            Trophies = GetAchievementTrophies();

            _model.Trophies.Clear();
            foreach (var trophy in Trophies)
            {
                _model.Trophies.Add(trophy);
            }

            TrophiesToAdd.CollectionChanged += OnTrophiesToAddChanged;

            CancelCommand = new Command(async () => await OnCancelAsync());
            ViewTrophiesCommand = new Command(async () => await OnViewTrophiesAsync());
            EditTagsCommand = new Command(async () => await EditTagsAsync());
            ClearTagsCommand = new Command(ClearTags);
            EditActiveTimeTargetCommand = new Command(async () => await EditActiveTimeTargetAsync());
            EditAchievementsCommand = new Command(async () => await EditAchievementsAsync());
            ClearAchievementsCommand = new Command(ClearAchievements);
            AddTrophyPhotoCommand = new Command(async () => await AddTrophyPhotoAsync());
            AddTrophyFileCommand = new Command(async () => await AddTrophyFileAsync());
            ClearTrophiesCommand = new Command(ClearTrophies);

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
            ActiveTimeTarget = ParseDuration(ActiveTimeTargetText);

            StepName = _model.StepName;
            AchievementTitle = _model.AchievementTitle;

            CompletionType = _model.CompletionType;

            RangeUnit = _model.RangeUnit;
            RangeAmountText = _model.RangeAmount.ToString(CultureInfo.InvariantCulture);

            var deadlineStart = _model.DeadlineStart ?? _model.CreatedDate;
            DeadlineStartDate = deadlineStart.Date;
            DeadlineStartTime = deadlineStart.TimeOfDay;

            var deadline = _model.Deadline ?? _clock.LocalNow;
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
                RaisePropertyChanged(nameof(IsStepTargetEmptyMessageVisible));
                RaisePropertyChanged(nameof(IsAchievementTargetEmptyMessageVisible));

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

        private DateTime _deadlineDate;
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

        private TimeSpan _deadlineTime;
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

        public bool IsStepTargetEmptyMessageVisible => IsStepTargetVisible && !StepNameOptions.Any();

        public bool IsAchievementTargetEmptyMessageVisible => IsAchievementTargetVisible && !_achievementTitleOptions.Any();


        // ===== Completion-type visibility =====

        public bool IsRangeVisible => CompletionType == AchievementCompletionType.Range;

        public bool IsDeadlineVisible => CompletionType == AchievementCompletionType.Deadline;


        // ===== Picker data =====

        public IReadOnlyList<AchievementTargetType> TargetTypeOptions { get; }
            = Enum.GetValues(typeof(AchievementTargetType))
                  .Cast<AchievementTargetType>()
                  .ToList();

        private async Task EditTagsAsync()
        {
            if (!CanEdit)
                return;

            var tags = await _detailsInteractions.PickTagsAsync(TagOptions, Tags, isReadOnly: true);
            if (tags != null)
                Tags = tags;
        }

        private void ClearTags()
        {
            if (CanEdit)
                Tags = "";
        }

        private async Task EditActiveTimeTargetAsync()
        {
            if (!CanEdit)
                return;

            var result = await _detailsInteractions.PickDurationAsync(ActiveTimeTarget);
            if (result is null)
                return;

            ActiveTimeTarget = result.Value;
            ActiveTimeTargetText = FormatDuration(result.Value, padSingleDigitHours: true);
        }

        private async Task EditAchievementsAsync()
        {
            if (!CanEdit)
                return;

            var achievementTitle = await _detailsInteractions.PickValuesAsync(
                "Select Achievements",
                _achievementTitleOptions,
                AchievementTitle,
                isReadOnly: true);

            if (achievementTitle != null)
                AchievementTitle = achievementTitle;
        }

        private void ClearAchievements()
        {
            if (CanEdit)
                AchievementTitle = "";
        }

        private async Task AddTrophyPhotoAsync()
        {
            if (!CanEdit)
                return;

            if (CompletionType == AchievementCompletionType.Range)
            {
                var paths = await _detailsInteractions.PickFilePathsAsync(
                    "Pick photos (trophies)",
                    FilePickerFileType.Images);

                AddPendingTrophies(paths);
                return;
            }

            var path = await _detailsInteractions.PickFilePathAsync(
                "Pick a photo (trophy)",
                FilePickerFileType.Images);

            ReplacePendingTrophy(path);
        }

        private async Task AddTrophyFileAsync()
        {
            if (!CanEdit)
                return;

            if (CompletionType == AchievementCompletionType.Range)
            {
                var paths = await _detailsInteractions.PickFilePathsAsync("Pick files (trophies)");
                AddPendingTrophies(paths);
                return;
            }

            var path = await _detailsInteractions.PickFilePathAsync("Pick a file (trophy)");
            ReplacePendingTrophy(path);
        }

        private void ClearTrophies()
        {
            if (!CanEdit)
                return;

            TrophiesToAdd.Clear();
            Trophies.Clear();
        }

        private void AddPendingTrophies(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                    TrophiesToAdd.Add(path);
            }
        }

        private void ReplacePendingTrophy(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            TrophiesToAdd.Clear();
            TrophiesToAdd.Add(path);
        }

        private static TimeSpan? ParseDuration(string? text)
        {
            var parts = (text ?? "")
                .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 3)
                return null;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours))
                return null;

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                return null;

            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                return null;

            return new TimeSpan(hours, 0, 0)
                + new TimeSpan(0, Math.Max(0, minutes), Math.Max(0, seconds));
        }

        private static string FormatDuration(TimeSpan duration, bool padSingleDigitHours = false)
        {
            var totalHours = (int)duration.TotalHours;
            var hourText = padSingleDigitHours && totalHours < 10
                ? $"0{totalHours}"
                : totalHours.ToString(CultureInfo.InvariantCulture);

            return $"{hourText}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        private async Task OnCancelAsync()
        {
            if (IsReadOnly)
            {
                await _navigation.PopAsync();
                return;
            }

            var choice = await _dialogs.DisplayActionSheetAsync(
                _model.Title,
                "Cancel",
                null,
                "Delete"
            );

            if (choice == "Delete")
            {
                _onDelete?.Invoke(_model);
                await _navigation.PopAsync();
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
                await _dialogs.DisplayAlertAsync("Invalid Achievement", ValidationMessage, "OK");
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

            await _navigation.PopAsync();
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
                await _dialogs.DisplayAlertAsync("Trophies", "No trophies saved.", "OK");
                return;
            }

            // ActionSheet supports a cancel + optional destruction button, and a list of options.
            // Note: ActionSheet is best for up to ~10-15 items; beyond that, use a modal page.
            var selected = await _dialogs.DisplayActionSheetAsync(
                "Trophies",
                "Close",
                null,
                trophies.ToArray());

            // Optional: If you want to do something when one is picked (copy/open/etc)
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
