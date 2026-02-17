using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class PlannerCreationViewModel : ObservableObject
    {
        public ObservableCollection<PlannerProgressRowVm> Rows { get; } = new();

        public PlannerCreationViewModel(List<IActiveCardModel> cards)
        {
            //Test
            cards = cards.Take(5).ToList();
            var testTimes = new[] 
            { 
                new { Spent = 2, Goal = new PlannerGoalDetailsModel() { TimeScope = TimeScope.Daily, GoalHrs = 4, DeFactoStart = new TimeOnly(7, 0), DeFactoEnd = new TimeOnly(22, 0) } }, 
                new { Spent = 4, Goal = new PlannerGoalDetailsModel() { TimeScope = TimeScope.Daily, GoalHrs = 4, DeFactoStart = new TimeOnly(7, 0), DeFactoEnd = new TimeOnly(22, 0) } },
                new { Spent = 3, Goal = new PlannerGoalDetailsModel() { TimeScope = TimeScope.Daily, GoalHrs = 6, DeFactoStart = new TimeOnly(7, 0), DeFactoEnd = new TimeOnly(22, 0) } },
                new { Spent = 4, Goal = new PlannerGoalDetailsModel() { TimeScope = TimeScope.Daily, GoalHrs = 3, DeFactoStart = new TimeOnly(7, 0), DeFactoEnd = new TimeOnly(22, 0) } },
                new { Spent = 1, Goal = new PlannerGoalDetailsModel() { TimeScope = TimeScope.Daily, GoalHrs = 2, DeFactoStart = new TimeOnly(7, 0), DeFactoEnd = new TimeOnly(22, 0) } },
            };

            int counter = 0;

            foreach (var card in cards)
            {
                card.Activity.Add(new ActivityModel() { StartDate = DateTime.Today, EndDate = DateTime.Today.AddHours(testTimes[counter].Spent) });
                counter++;
            }

            counter = 0;

            List<PlannerProgressRowVm> pprvms = new List<PlannerProgressRowVm>();
            foreach (var card in cards)
            {
                pprvms.Add(new PlannerProgressRowVm(card, testTimes[counter].Goal));
                counter++;
            }

            var maxValue = pprvms.Max(x => Math.Max(x.TotalValue, (x.CurrentValue.HasValue ? x.CurrentValue.Value : 0)));

            foreach (var pprvm in pprvms)
            {
                pprvm.MaxValue = maxValue;
                Rows.Add(pprvm);
            }

            //Rows.Add(new PlannerProgressRowVm
            //{
            //    LeftText = "Health",
            //    RightTopText = "3pts",
            //    RightBottomText = "20%",
            //    MaxValue = 100,
            //    TotalValue = 60,
            //    CurrentValue = 25,
            //    ExpectedValue = 40,
            //    TrackColor = Color.FromArgb("#2A2A2A"),
            //    TotalColor = Color.FromArgb("#3B82F6"),
            //    CurrentColor = Color.FromArgb("#22C55E"),
            //    ExpectedLineColor = Colors.White,
            //    ShowBarLabels = true // probably off here since you already have right-side labels
            //});

            //Rows.Add(new PlannerProgressRowVm
            //{
            //    LeftText = "Coding",
            //    RightTopText = "8pts",
            //    RightBottomText = "53%",
            //    MaxValue = 15,
            //    TotalValue = 12,
            //    CurrentValue = 8,
            //    ExpectedValue = 10,
            //    TotalColor = Color.FromArgb("#A855F7"),
            //    CurrentColor = Color.FromArgb("#F59E0B"),
            //    ShowBarLabels = true
            //});
        }
    }

    public sealed class PlannerProgressRowVm
    {
        // Left / right labels
        public string LeftText { get; init; } = "";
        public string RightTopText { get; init; } = "";
        public string RightBottomText { get; init; } = "";

        // Values
        public double MaxValue { get; set; } = 100;
        public double TotalValue { get; init; }
        public double? CurrentValue { get; init; }
        public double? ExpectedValue { get; init; }

        // Optional features
        public bool ShowCurrentOverlay { get; init; } = true;
        public bool ShowExpectedMarker { get; init; } = true;
        public bool ShowBarLabels { get; init; } = true;

        public float BarLabelFontSize { get; init; } = 12f;
        public float BarLabelOffset { get; init; } = 6f;


        // Bar appearance
        public float BarThickness { get; init; } = 16f;
        public float BarTotalHeight { get; init; } = 64f; // enough for labels + bar


        public Color TrackColor { get; init; } = Color.FromArgb("#2A2A2A");
        public Color TotalColor { get; init; } = Color.FromArgb("#3B82F6");
        public Color CurrentColor { get; init; } = Color.FromArgb("#22C55E");
        public Color ExpectedLineColor { get; init; } = Colors.White;

        public string BarLabelFormat { get; init; } = "0";

        public PlannerProgressRowVm(IActiveCardModel card, PlannerGoalDetailsModel plannerGoalDetailsModel)
        {

            var pts = GetTotalGoalPoints(card, plannerGoalDetailsModel.GoalHrs);
            var pcTotalTime = GetPercentOfTotalTime(plannerGoalDetailsModel.GoalHrs, plannerGoalDetailsModel.TimeScope, DateTime.Now);
            var maxHrs = GetMaxHours(plannerGoalDetailsModel.TimeScope, DateTime.Now);
            var currentHrs = GetTotalCurrentHoursSpent(card, plannerGoalDetailsModel.TimeScope, DateTime.Now);
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

        private double? GetTotalExpectedByNowHoursSpent(double totalGoalHrs, TimeScope timeScope, DateTime now)
        {
            var range = new TimeScopeRange(timeScope, now);

            var pcToHaveComplete = range.GetPercentageComplete(now);

            return totalGoalHrs * (pcToHaveComplete / 100);
        }

        private double? GetTotalCurrentHoursSpent(IActiveCardModel card, TimeScope timeScope, DateTime now)
        {
            var range = new TimeScopeRange(timeScope, now);

            return card.GetActiveTime(range.Start, range.End).Hours;
        }

        private double GetMaxHours(TimeScope timeScope, DateTime now)
        {
            var range = new TimeScopeRange(timeScope, now);

            return (range.End - range.Start).Hours;
        }

        private double GetPercentOfTotalTime(double totalGoalHrs, TimeScope timeScope, DateTime now)
        {
            var range = new TimeScopeRange(timeScope, now);

            return totalGoalHrs / (range.End - range.Start).Hours;
        }

        private double GetTotalGoalPoints(IActiveCardModel card, double totalGoalHrs)
        {
            var lowestVPM = card.ValuePerMinute;

            return (totalGoalHrs * 60) * lowestVPM;
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

    public class PlannerGoalDetailsModel
    {
        public TimeScope TimeScope { get; set; }

        public double GoalHrs { get; set; }

        public TimeOnly? DeFactoStart { get; set; }
        public TimeOnly? DeFactoEnd { get; set; }

    }
}
