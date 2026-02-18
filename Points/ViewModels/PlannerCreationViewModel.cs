using Points.Global;
using Points.Models;
using Points.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class PlannerCreationViewModel : ObservableObject
    {
        public List<string> PeriodOptions { get; } = new() { "Daily", "Weekly", "Monthly" };

        private string _selectedPeriod = "Daily";
        private IDbService _db;

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (_selectedPeriod == value) return;
                _selectedPeriod = value;
                RaisePropertyChanged(SelectedPeriod); // if using INotifyPropertyChanged

                // Optional: trigger recalculation logic here
                _ = ReloadAsync();
            }
        }

        public Command SaveCommand { get; }

        public ObservableCollection<PlannerProgressRowVm> Rows { get; } = new();

        public Task? Initialization { get; private set; }

        public PlannerCreationViewModel(IDbService db)
        {
            _db = db;

            SaveCommand = new Command(async () => await SaveAsync());

            Initialization = ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            Rows.Clear();
            await LoadAsync();
        }

        private List<IActiveCardModel>? _cards { get; set; }
        private List<PlannerGoalDetailsModel>? _plannerModels { get; set; }

        private async Task LoadAsync()
        {
            if(Enum.TryParse(typeof(TimeScope), _selectedPeriod, true, out object tscope))
            {
                var range = new TimeScopeRange((TimeScope)tscope, DateTime.Now);

                if(_cards == null) _cards = await _db.GetMainQuestModelsDataAsync(range.Start, range.End);

                if (_plannerModels == null) _plannerModels = await _db.GetPlannerModelsDataAsync();

                var plannerModels = _plannerModels.Where(x => x.TimeScope == (TimeScope)tscope).ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    List<PlannerProgressRowVm> pprvms = new List<PlannerProgressRowVm>();
                    foreach (var card in _cards)
                    {
                        var plannerModel = plannerModels.Any(x => x.CardId == card.CardID) ? plannerModels.First(x => x.CardId == card.CardID) : new PlannerGoalDetailsModel() { CardId = card.CardID };

                        var row = new PlannerProgressRowVm(card, plannerModel);
                        row.EnableCheckbox = true;
                        row.IsChecked = plannerModel.Enabled;
                        pprvms.Add(row);
                    }

                    var maxValue = pprvms.Max(x => Math.Max(x.TotalValue, (x.CurrentValue.HasValue ? x.CurrentValue.Value : 0)));

                    foreach (var pprvm in pprvms)
                    {
                        pprvm.MaxValue = maxValue;
                        Rows.Add(pprvm);
                    }

                });
            }
        }

        private async Task SaveAsync()
        {
            List<PlannerGoalDetailsModel> plannerModelsToSave = new List<PlannerGoalDetailsModel>();
            foreach (var row in Rows)
            {
                if(row.TotalValue > 0)
                {
                    if(Enum.TryParse(typeof(TimeScope), _selectedPeriod, true, out object tscope))
                    {
                        row.PlannerGoalDetailsModel.TimeScope = (TimeScope)tscope;
                    }
                    row.PlannerGoalDetailsModel.DeFactoStart = row.UseDeFactoTimes ? row.DeFactoStartTime : null;
                    row.PlannerGoalDetailsModel.DeFactoEnd = row.UseDeFactoTimes ? row.DeFactoEndTime : null;
                    row.PlannerGoalDetailsModel.GoalHrs = row.TotalValue;
                    row.PlannerGoalDetailsModel.Enabled = row.IsChecked;

                    if ((row.UseDeFactoTimes && row.DeFactoStartTime < row.DeFactoEndTime) || !row.UseDeFactoTimes)
                    {
                        plannerModelsToSave.Add(row.PlannerGoalDetailsModel);
                    }             
                }
            }

            //TODO: Save to DB
            await _db.SavePlannerModelsDataAsync(plannerModelsToSave);

            await Shell.Current.Navigation.PopAsync();
        }
    }



    public sealed class PlannerProgressRowVm : ObservableObject, ICardModel
    {
        // Left / right labels
        public string LeftText { get; init; } = "";
        public string RightTopText { get; init; } = "";
        public string RightBottomText { get; init; } = "";

        // Values
        public double MaxValue { get; set; } = 100;

        private double _totalValue;
        public double TotalValue { get => _totalValue; set { _totalValue = value; RaisePropertyChanged(nameof(TotalValue)); } }
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
                ExpectedValue = GetTotalExpectedByNowHoursSpent(PlannerGoalDetailsModel.GoalHrs, PlannerGoalDetailsModel.TimeScope, DateTime.Now);
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
                ExpectedValue = GetTotalExpectedByNowHoursSpent(PlannerGoalDetailsModel.GoalHrs, PlannerGoalDetailsModel.TimeScope, DateTime.Now);
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
                ExpectedValue = GetTotalExpectedByNowHoursSpent(PlannerGoalDetailsModel.GoalHrs, PlannerGoalDetailsModel.TimeScope, DateTime.Now);
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

        public PlannerGoalDetailsModel PlannerGoalDetailsModel { get; set; }
        public int Id { get; set; }

        public long CardID { get; set; }

        public string Title { get; set; }

        public string Tags { get; set; }

        public PlannerProgressRowVm(IActiveCardModel card, PlannerGoalDetailsModel plannerGoalDetailsModel)
        {
            Id = card.Id;
            CardID = card.CardID;
            this.Title = card.Title;
            this.Tags = card.Tags;

            PlannerGoalDetailsModel = plannerGoalDetailsModel;

            if (plannerGoalDetailsModel.DeFactoStart.HasValue) DeFactoStartTime = plannerGoalDetailsModel.DeFactoStart.Value;
            if (plannerGoalDetailsModel.DeFactoEnd.HasValue) DeFactoEndTime = plannerGoalDetailsModel.DeFactoEnd.Value;

            if (plannerGoalDetailsModel.DeFactoStart.HasValue || plannerGoalDetailsModel.DeFactoEnd.HasValue) UseDeFactoTimes = true;

            if(card is ScCardModel sc)
            {
                var currentValue = GetTotalCurrentValueEarned(sc, plannerGoalDetailsModel, DateTime.Now);
                var expectedByNowValue = GetTotalExpectedByNowHoursSpent(plannerGoalDetailsModel.GoalHrs, plannerGoalDetailsModel.TimeScope, DateTime.Now);

                LeftText = card.Title;
                RightTopText = plannerGoalDetailsModel.GoalHrs + "pts";
                RightBottomText = "";
                MaxValue = plannerGoalDetailsModel.GoalHrs;
                TotalValue = plannerGoalDetailsModel.GoalHrs;
                CurrentValue = currentValue;
                ExpectedValue = expectedByNowValue;
                TotalColor = Color.FromArgb("#A855F7");
                CurrentColor = Color.FromArgb("#F59E0B");
                ShowBarLabels = true;
            }
            else if(card is TatCardModel tat)
            {
                var pts = GetTotalGoalPoints(card, plannerGoalDetailsModel.GoalHrs);
                var pcTotalTime = GetPercentOfTotalTime(plannerGoalDetailsModel.GoalHrs, plannerGoalDetailsModel, DateTime.Now);
                var maxHrs = GetMaxHours(plannerGoalDetailsModel, DateTime.Now);
                var currentHrs = GetTotalCurrentHoursSpent(card, plannerGoalDetailsModel, DateTime.Now);
                var expectedByNowHrs = GetTotalExpectedByNowHoursSpent(plannerGoalDetailsModel.GoalHrs, plannerGoalDetailsModel.TimeScope, DateTime.Now);

                LeftText = card.Title;
                RightTopText = Math.Round(pts, 1) + "pts";
                RightBottomText = Math.Round(pcTotalTime, 1) + "%";
                MaxValue = maxHrs;
                TotalValue = plannerGoalDetailsModel.GoalHrs;
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

        private double? GetTotalCurrentHoursSpent(IActiveCardModel card, PlannerGoalDetailsModel plannerGoalDetailsModel, DateTime now)
        {
            var range = new TimeScopeRange(plannerGoalDetailsModel.TimeScope, now);

            return card.GetActiveTime(range.Start, range.End).TotalHours;
        }

        private double GetTotalCurrentValueEarned(ScCardModel card, PlannerGoalDetailsModel plannerGoalDetailsModel, DateTime now)
        {
            var range = new TimeScopeRange(plannerGoalDetailsModel.TimeScope, now);

            return card.GetValue(range.Start, range.End);
        }

        private double GetMaxHours(PlannerGoalDetailsModel plannerGoalDetailsModel, DateTime now)
        {
            var range = new TimeScopeRange(plannerGoalDetailsModel.TimeScope, now);

            if (plannerGoalDetailsModel.DeFactoStart.HasValue) range.Start = DateOnly.FromDateTime(range.Start).ToDateTime(plannerGoalDetailsModel.DeFactoStart.Value, range.Start.Kind);
            if (plannerGoalDetailsModel.DeFactoEnd.HasValue) range.End = DateOnly.FromDateTime(range.End).ToDateTime(plannerGoalDetailsModel.DeFactoEnd.Value, range.End.Kind);

            return (range.End - range.Start).TotalHours;
        }

        private double GetPercentOfTotalTime(double totalGoalHrs, PlannerGoalDetailsModel plannerGoalDetailsModel, DateTime now)
        {
            var range = new TimeScopeRange(plannerGoalDetailsModel.TimeScope, now);

            if (plannerGoalDetailsModel.DeFactoStart.HasValue) range.Start = DateOnly.FromDateTime(range.Start).ToDateTime(plannerGoalDetailsModel.DeFactoStart.Value, range.Start.Kind);
            if (plannerGoalDetailsModel.DeFactoEnd.HasValue) range.End = DateOnly.FromDateTime(range.End).ToDateTime(plannerGoalDetailsModel.DeFactoEnd.Value, range.End.Kind);

            return (totalGoalHrs / (range.End - range.Start).TotalHours) * 100;
        }

        private double GetTotalGoalPoints(IActiveCardModel card, double totalGoalHrs)
        {
            return (totalGoalHrs * 60) * card.ValuePerMinute;
        }



        public double GetValue(DateTime start, DateTime end)
        {
            return 0;
        }
    }

    public enum TimeScope
    {
        Daily, Weekly, Monthly
    }

    public class TimeScopeRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public TimeScopeRange(TimeScope timeScope, DateTime now)
        {
            switch (timeScope)
            {
                case TimeScope.Daily:
                    Start = now.Date;
                    End = now.Date.AddDays(1).AddSeconds(-1);
                    break;
                case TimeScope.Weekly:
                    {
                        // ISO 8601: Monday = first day of week
                        int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                        Start = now.Date.AddDays(-diff);
                        End = Start.AddDays(7).AddSeconds(-1);
                        break;
                    }

                case TimeScope.Monthly:
                    {
                        Start = new DateTime(now.Year, now.Month, 1);
                        End = Start.AddMonths(1).AddSeconds(-1);
                        break;
                    }
                default:
                    break;
            }
        }

        public double GetPercentageComplete(DateTime atTime)
        {
            if (atTime > End) return 100;

            if (atTime < Start) return 0;

            var total = (End - Start).TotalMilliseconds;
            if (total <= 0) return 100d; // degenerate range; treat as complete

            var elapsed = (atTime - Start).TotalMilliseconds;

            var pct = (elapsed / total) * 100d;

            // Defensive clamp for rounding/clock drift
            if (pct < 0d) return 0d;
            if (pct > 100d) return 100d;
            return pct;
        }
    }
}
