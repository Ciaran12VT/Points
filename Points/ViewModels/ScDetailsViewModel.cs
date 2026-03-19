
using Points.Global;
using Points.Models;
using Points.Services.Sqlite.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Points.ViewModels
{
    public class ScDetailsViewModel : ObservableObject
    {
        private ScCardModel _model;
        private Action<ScCardModel> _onSaved;
        private Action<ScCardModel> _onDelete;

        public Command CancelCommand { get; }
        public List<string> AvailableTagList { get; }
        public ObservableCollection<ScStepModel> Steps { get; } = new();

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        private double initalTotalValue = 0;

        public ScDetailsViewModel(ScCardModel model, Action<ScCardModel> onSaved, Action<ScCardModel> onDelete, List<string> availableTagsList, IDbService db)
        {
            _db = db;
            ToggleSignCommand = new Command(ToggleSign);
            AddStepCommand = new Command(AddStep);
            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await OnCancelAsync());
            AvailableTagList = availableTagsList;
            _onSaved = onSaved;
            _onDelete = onDelete;

            // Tick every second
            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, __) =>
            {
                RaisePropertyChanged(nameof(ActiveTimeText));
            };
            _timer.Start();

            BuildModel(model);
        }

        private void BuildModel(ScCardModel model)
        {
            _model = model;

            // Load editable fields
            Title = _model.Title;
            Tags = _model.Tags;
            Description = _model.Description;

            // Sign is stored in ValuePerMinute sign (magnitude not used for SC)
            _isNegative = _model.ValuePerMinute < 0;

            // Copy steps into a local collection (edit freely, commit on save)
            foreach (var s in _model.Steps.OrderBy(x => x.SortOrder))
            {
                var step = s ?? new ScStepModel { SortOrder = Steps.Count + 1, StepValue = 1.0 };
                HookStep(step);
                Steps.Add(step);
            }

            if (Steps.Count == 0)
                AddStep();

            RaiseComputed();

            var sum = Steps.Sum(s => s.StepValue * s.Count(GlobalVariables.RangeStart, GlobalVariables.RangeEnd));
            var signed = (_isNegative ? -1 : 1) * sum;
            initalTotalValue = signed;
        }

        private DateTime _rangeStart = GlobalVariables.RangeStart;
        public DateTime RangeStart
        {
            get => _rangeStart;
            set
            {
                if (_rangeStart == value) return;
                _rangeStart = value;
                RaisePropertyChanged();
            }
        }

        private DateTime _rangeEnd = GlobalVariables.RangeEnd;
        public DateTime RangeEnd
        {
            get => _rangeEnd;
            set
            {
                if (_rangeEnd == value) return;
                _rangeEnd = value;
                RaisePropertyChanged();
            }
        }

        // Editable fields
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        // Read-only display fields
        public string Status => _model.Status;

        public string ActiveTimeText
            => _model.GetActiveTime(GlobalVariables.RangeStart, GlobalVariables.RangeEnd).ToString(@"hh\:mm\:ss");

        public string CurrentAccruedValueText
        {
            get
            {
                var sum = Steps.Sum(s => s.StepValue * s.Count(GlobalVariables.RangeStart, GlobalVariables.RangeEnd));
                var signed = (_isNegative ? -1 : 1) * sum;
                return signed.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        public Color CurrentAccruedValueColor
        {
            get
            {
                if (!double.TryParse(CurrentAccruedValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return Colors.Green;
                return v < 0 ? Colors.Red : Colors.Green;
            }
        }

        // Sign toggle
        private bool _isNegative;
        public string SignToggleText => _isNegative ? "-" : "+";
        public string SignToggleColor => _isNegative ? "Red" : "Green";

        private IDbService _db;

        public Command ToggleSignCommand { get; }
        private void ToggleSign()
        {
            _isNegative = !_isNegative;
            RaisePropertyChanged(nameof(SignToggleText));
            RaisePropertyChanged(nameof(SignToggleColor));
            RaiseComputed();
        }

        public Command AddStepCommand { get; }
        private void AddStep()
        {
            var step = new ScStepModel { SortOrder = Steps.Count + 1, StepValue = 1.0 };
            HookStep(step);
            Steps.Add(step);

            RaiseComputed();
        }

        public Command SaveCommand { get; }

        private async Task SaveAsync()
        {
            // Commit simple fields
            _model.Title = Title;
            _model.Tags = Tags;
            _model.Description = Description;

            // Commit sign using ValuePerMinute sign
            _model.ValuePerMinute = _isNegative ? -1 : 1;

            // Commit steps back to model
            _model.Steps.Clear();
            int order = 1;
            foreach (var s in Steps)
            {
                _model.Steps.Add(new ScStepModel
                {
                    Id = s.Id,
                    SortOrder = order++,
                    Title = s.Title,
                    StepValue = s.StepValue,
                    Reps = s.Reps
                });
            }

            _model.TimeValueAchievementEvaluators = await _db.RefreshEvaluatorsAsync(_model.TimeValueAchievementEvaluators);

            await EvaluateAchievements(_model);

            if (_onSaved != null) _onSaved(_model);

            await Shell.Current.Navigation.PopAsync();
        }

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

        private void RaiseComputed()
        {
            RaisePropertyChanged(nameof(CurrentAccruedValueText));
            RaisePropertyChanged(nameof(CurrentAccruedValueColor));
            RaisePropertyChanged(nameof(ActiveTimeText));
        }

        private void HookStep(ScStepModel step)
        {
            step.PropertyChanged += (_, __) => RaiseComputed();
        }

        private async Task EvaluateAchievements(ScCardModel model)
        {
            // Compute the delta since the page was opened
            var sum = Steps.Sum(s => s.StepValue * s.Count(GlobalVariables.RangeStart, GlobalVariables.RangeEnd));
            var signed = (_isNegative ? -1 : 1) * sum;
            var amountToAdd = signed - initalTotalValue;

            // If user reduced value or made no change, don't evaluate earns.
            if (amountToAdd <= 0)
                return;

            if (model.TimeValueAchievementEvaluators is null || model.TimeValueAchievementEvaluators.Count == 0)
                return;

            // Gather earned achievements, safely handling null returns, and dedupe by Id
            var earned = model.TimeValueAchievementEvaluators
                .SelectMany(e => e.CheckForEarnedAchievements(0, amountToAdd) ?? Enumerable.Empty<AchievementCardModel>())
                .GroupBy(a => a.Id)
                .Select(g => g.First())
                .ToList();

            if (earned.Count == 0)
                return;

            // 1) Persist (DB)
            var now = DateTime.Now;
            foreach (var ach in earned)
            {
                await _db.MarkAchievementEarnedAsync(ach.Id, now);

                // If you keep this field in-memory, update it as well
                ach.LastEarnedAt = now;
            }

            // 2) Notify user (UI)
            // Prefer one dialog rather than N dialogs
            var msg = earned.Count == 1
                ? $"You earned: {earned[0].Title}"
                : "You earned:\n• " + string.Join("\n• ", earned.Select(a => a.Title));

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlert("Achievement unlocked", msg, "Nice");
            });
        }

    }
}
