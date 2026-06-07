using Points.Global;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Scheduling;
using Points.Services.Persistence;
using Points.Services.Time;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels.Goals
{
    public class GoalCreationViewModel : ObservableObject
    {
        public List<string> PeriodOptions { get; } = new() { "Daily", "Weekly", "Monthly" };

        private string _selectedPeriod = "Daily";
        private readonly ICardReadService _cardReader;
        private readonly IGoalService _goals;
        private readonly IAppNavigationService _navigation;
        private readonly IClock _clock;

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (_selectedPeriod == value) return;
                _selectedPeriod = value;
                RaisePropertyChanged(nameof(SelectedPeriod));

                // Optional: trigger recalculation logic here
                _ = ReloadAsync();
            }
        }

        public Command SaveCommand { get; }

        public ObservableCollection<GoalProgressRowVm> Rows { get; } = new();

        public Task? Initialization { get; private set; }

        public GoalCreationViewModel(
            ICardReadService cardReader,
            IGoalService goals,
            IAppNavigationService navigation,
            IClock? clock = null)
        {
            _cardReader = cardReader;
            _goals = goals;
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _clock = clock ?? new SystemClock();

            SaveCommand = new Command(async () => await SaveAsync());

            Initialization = ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            Rows.Clear();
            await LoadAsync();
        }

        private List<IActiveCardModel>? _cards { get; set; }
        private List<GoalDetailsModel>? _goalModels { get; set; }

        private async Task LoadAsync()
        {
            if(Enum.TryParse<TimeScope>(_selectedPeriod, true, out var tscope))
            {
                var now = LocalNow;

                List<DateTime> startDates = new()
                {
                    new TimeScopeRange(TimeScope.Daily, now).Start,
                    new TimeScopeRange(TimeScope.Weekly, now).Start,
                    new TimeScopeRange(TimeScope.Monthly, now).Start
                };
                List<DateTime> endDates = new()
                {
                    new TimeScopeRange(TimeScope.Daily, now).End,
                    new TimeScopeRange(TimeScope.Weekly, now).End,
                    new TimeScopeRange(TimeScope.Monthly, now).End
                };

                if (_cards == null) _cards = await _cardReader.GetMainQuestModelsDataAsync(startDates.Min(), endDates.Max());

                if (_goalModels == null) _goalModels = await _goals.GetGoalModelsDataAsync();

                var goalModels = _goalModels.Where(x => x.TimeScope == tscope).ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    List<GoalProgressRowVm> rowVms = new List<GoalProgressRowVm>();
                    foreach (var card in _cards)
                    {
                        var goalModel = goalModels.Any(x => x.CardId == card.CardID) ? goalModels.First(x => x.CardId == card.CardID) : new GoalDetailsModel() { CardId = card.CardID };

                        var row = new GoalProgressRowVm(card, goalModel, () => _clock.LocalNow);
                        row.EnableCheckbox = true;
                        row.IsChecked = goalModel.Enabled;
                        rowVms.Add(row);
                    }

                    foreach (var rowVm in rowVms)
                    {
                        Rows.Add(rowVm);
                    }

                });
            }
        }

        private async Task SaveAsync()
        {
            List<GoalDetailsModel> goalModelsToSave = new List<GoalDetailsModel>();
            foreach (var row in Rows)
            {
                if(row.TotalValue > 0)
                {
                    if(Enum.TryParse<TimeScope>(_selectedPeriod, true, out var tscope))
                    {
                        row.GoalDetailsModel.TimeScope = tscope;
                    }
                    row.GoalDetailsModel.DeFactoStart = row.UseDeFactoTimes ? row.DeFactoStartTime : null;
                    row.GoalDetailsModel.DeFactoEnd = row.UseDeFactoTimes ? row.DeFactoEndTime : null;
                    row.GoalDetailsModel.GoalHrs = row.TotalValue;
                    row.GoalDetailsModel.Enabled = row.IsChecked;

                    if ((row.UseDeFactoTimes && row.DeFactoStartTime < row.DeFactoEndTime) || !row.UseDeFactoTimes)
                    {
                        goalModelsToSave.Add(row.GoalDetailsModel);
                    }             
                }
            }

            //TODO: Save to DB
            await _goals.SaveGoalModelsDataAsync(goalModelsToSave);

            await _navigation.PopAsync();
        }

        private DateTime LocalNow => WallClockScheduleTime.NormalizeLocal(_clock.LocalNow);
    }



    public sealed class GoalProgressRowVm : ObservableObject, ICardModel
    {
        // Left / right labels
        public string LeftText { get; init; } = "";
        public string RightTopText { get; init; } = "";
        public string RightBottomText { get; init; } = "";

        // Values
        private double _maxValue = 100;
        public double MaxValue { get => _maxValue; set => SetProperty(ref _maxValue, value); }

        private double _totalValue;
        public double TotalValue { get => _totalValue; set { if (SetProperty(ref _totalValue, value)) MaxValue = value; } }
        public double? CurrentValue { get; init; }

        private double? _expectedValue;
        public double? ExpectedValue { get => _expectedValue; set { _expectedValue = value; RaisePropertyChanged(nameof(ExpectedValue)); } }

        // Optional features
        public bool ShowCurrentOverlay { get; init; } = true;
        public bool ShowExpectedMarker { get; init; } = true;
        public bool ShowBarLabels { get; init; } = true;

        public float BarLabelFontSize { get; init; } = 12f;
        public float BarLabelOffset { get; init; } = 6f;


        // Bar appearance
        public float BarThickness { get; init; } = 16f;
        public float BarTotalHeight { get; init; } = 64f; // enough for labels + bar

        public bool EnableCheckbox { get; set; } = false;
        public bool IsChecked { get; set; }


        public Color TrackColor { get; init; } = Color.FromArgb("#2A2A2A");
        public Color TotalColor { get; init; } = Color.FromArgb("#3B82F6");
        public Color CurrentColor { get; init; } = Color.FromArgb("#22C55E");
        public Color ExpectedLineColor { get; init; } = Colors.White;

        public string BarLabelFormat { get; init; } = "0.0";


        private TimeOnly _deFactoStartTime = new(9, 0);
        public TimeOnly DeFactoStartTime 
        {
            get => _deFactoStartTime; 
            set 
            { 
                _deFactoStartTime = value;
                ExpectedValue = GetTotalExpectedByNowHoursSpent(GoalDetailsModel.GoalHrs, GoalDetailsModel.TimeScope, LocalNow);
                RaisePropertyChanged(nameof(DeFactoStartTime));
                RaisePropertyChanged(nameof(DeFactoStartTimeSpan)); 
            } 
        }

        private TimeOnly _deFactoEndTime = new(17, 0);
        public TimeOnly DeFactoEndTime 
        { 
            get => _deFactoEndTime; 
            set 
            { 
                _deFactoEndTime = value;
                ExpectedValue = GetTotalExpectedByNowHoursSpent(GoalDetailsModel.GoalHrs, GoalDetailsModel.TimeScope, LocalNow);
                RaisePropertyChanged(nameof(DeFactoEndTime));
                RaisePropertyChanged(nameof(DeFactoEndTimeSpan)); 
            } 
        }

        private bool _useDeFactoTimes;
        public bool UseDeFactoTimes 
        { 
            get => _useDeFactoTimes; 
            set 
            { 
                _useDeFactoTimes = value;
                ExpectedValue = GetTotalExpectedByNowHoursSpent(GoalDetailsModel.GoalHrs, GoalDetailsModel.TimeScope, LocalNow);
                RaisePropertyChanged(nameof(UseDeFactoTimes));            
            } 
        }


        // --- Bridge properties for TimePicker (bind these) ---
        public TimeSpan DeFactoStartTimeSpan
        {
            get => DeFactoStartTime.ToTimeSpan();
            set => DeFactoStartTime = TimeOnly.FromTimeSpan(value);
        }

        public TimeSpan DeFactoEndTimeSpan
        {
            get => DeFactoEndTime.ToTimeSpan();
            set => DeFactoEndTime = TimeOnly.FromTimeSpan(value);
        }

        public GoalDetailsModel GoalDetailsModel { get; set; }
        public int Id { get; set; }

        public long CardID { get; set; }
        public int DisplayOrder { get; set; }

        public string Title { get; set; }

        public string Tags { get; set; }

        private readonly Func<DateTime> _localNowProvider;

        private DateTime LocalNow => WallClockScheduleTime.NormalizeLocal(_localNowProvider());

        public GoalProgressRowVm(
            IActiveCardModel card,
            GoalDetailsModel goalDetailsModel,
            Func<DateTime>? localNowProvider = null)
        {
            _localNowProvider = localNowProvider ?? (() => ActivityTimeMath.LocalNow);

            Id = card.Id;
            CardID = card.CardID;
            DisplayOrder = card.DisplayOrder;
            this.Title = card.Title;
            this.Tags = card.Tags;

            GoalDetailsModel = goalDetailsModel;

            if (goalDetailsModel.DeFactoStart.HasValue) DeFactoStartTime = goalDetailsModel.DeFactoStart.Value;
            if (goalDetailsModel.DeFactoEnd.HasValue) DeFactoEndTime = goalDetailsModel.DeFactoEnd.Value;

            if (goalDetailsModel.DeFactoStart.HasValue || goalDetailsModel.DeFactoEnd.HasValue) UseDeFactoTimes = true;

            if(card is ScCardModel sc)
            {
                var now = LocalNow;
                var currentValue = GetTotalCurrentValueEarned(sc, goalDetailsModel, now);
                var expectedByNowValue = GetTotalExpectedByNowHoursSpent(goalDetailsModel.GoalHrs, goalDetailsModel.TimeScope, now);

                LeftText = card.Title;
                RightTopText = goalDetailsModel.GoalHrs + "pts";
                RightBottomText = "";
                MaxValue = goalDetailsModel.GoalHrs;
                TotalValue = goalDetailsModel.GoalHrs;
                CurrentValue = currentValue;
                ExpectedValue = expectedByNowValue;
                TotalColor = Color.FromArgb("#A855F7");
                CurrentColor = Color.FromArgb("#F59E0B");
                ShowBarLabels = true;
            }
            else if(card is TatCardModel tat)
            {
                var now = LocalNow;
                var pts = GetTotalGoalPoints(card, goalDetailsModel.GoalHrs);
                var pcTotalTime = GetPercentOfTotalTime(goalDetailsModel.GoalHrs, goalDetailsModel, now);
                var currentHrs = GetTotalCurrentHoursSpent(card, goalDetailsModel, now);
                var expectedByNowHrs = GetTotalExpectedByNowHoursSpent(goalDetailsModel.GoalHrs, goalDetailsModel.TimeScope, now);

                LeftText = card.Title;
                RightTopText = Math.Round(pts, 1) + "pts";
                RightBottomText = Math.Round(pcTotalTime, 1) + "%";
                MaxValue = goalDetailsModel.GoalHrs;
                TotalValue = goalDetailsModel.GoalHrs;
                CurrentValue = currentHrs;
                ExpectedValue = expectedByNowHrs;
                TotalColor = Color.FromArgb("#A855F7");
                CurrentColor = Color.FromArgb("#F59E0B");
                ShowBarLabels = true;
            }
        }

        private double? GetTotalExpectedByNowHoursSpent(double totalGoalHrs, TimeScope tscope, DateTime now)
        {
            var range = new TimeScopeRange(tscope, now);

            if (UseDeFactoTimes) range.Start = DateOnly.FromDateTime(range.Start).ToDateTime(DeFactoStartTime, range.Start.Kind);
            if (UseDeFactoTimes) range.End = DateOnly.FromDateTime(range.End).ToDateTime(DeFactoEndTime, range.End.Kind);

            var pcToHaveComplete = range.GetPercentageComplete(now);

            return totalGoalHrs * (pcToHaveComplete / 100);
        }

        private double? GetTotalCurrentHoursSpent(IActiveCardModel card, GoalDetailsModel goalDetailsModel, DateTime now)
        {
            var range = new TimeScopeRange(goalDetailsModel.TimeScope, now);

            return card.GetActiveTime(range.Start, range.End).TotalHours;
        }

        private double GetTotalCurrentValueEarned(ScCardModel card, GoalDetailsModel goalDetailsModel, DateTime now)
        {
            var range = new TimeScopeRange(goalDetailsModel.TimeScope, now);

            return MultiplierValueCalculator.GetValue(card, range.Start, range.End);
        }

        private double GetPercentOfTotalTime(double totalGoalHrs, GoalDetailsModel goalDetailsModel, DateTime now)
        {
            var range = new TimeScopeRange(goalDetailsModel.TimeScope, now);

            if (goalDetailsModel.DeFactoStart.HasValue) range.Start = DateOnly.FromDateTime(range.Start).ToDateTime(goalDetailsModel.DeFactoStart.Value, range.Start.Kind);
            if (goalDetailsModel.DeFactoEnd.HasValue) range.End = DateOnly.FromDateTime(range.End).ToDateTime(goalDetailsModel.DeFactoEnd.Value, range.End.Kind);

            return (totalGoalHrs / (range.End - range.Start).TotalHours) * 100;
        }

        private double GetTotalGoalPoints(IActiveCardModel card, double totalGoalHrs)
        {
            return MultiplierValueCalculator.ApplyToCard(card, (totalGoalHrs * 60) * card.ValuePerMinute);
        }



        public double GetValue(DateTime start, DateTime end)
        {
            return 0;
        }
    }
}
