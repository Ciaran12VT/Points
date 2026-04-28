using Points.Models;
using Points.Views.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Points.ViewModels
{
    internal class EditLocksVm
    {
        private readonly long _cardId;
        private readonly List<DependencyTaskOption> _taskOptions;

        public ObservableCollection<LockEditorVm> Locks { get; } = new();

        public EditLocksVm(long cardId, IEnumerable<LockModel> initial, List<DependencyTaskOption> taskOptions)
        {
            _cardId = cardId;
            _taskOptions = taskOptions;

            foreach (var l in initial.OrderBy(x => x.LockNumber))
                Locks.Add(new LockEditorVm(l, _taskOptions));
        }

        public void AddLock()
        {
            var next = Locks.Count == 0 ? 1 : Locks.Max(x => x.Model.LockNumber) + 1;

            var model = new LockModel
            {
                CardId = _cardId,
                LockNumber = next,
                // defaults per spec
                TimeWindowStart = new TimeOnly(0, 0, 0),
                TimeWindowEnd = new TimeOnly(23, 59, 59),
                Schedules = new List<LockScheduleModel>
            {
                // sensible default: Once "today"
                new LockScheduleModel
                {
                    FrequencyType = FrequencyType.Once,
                    FrequencyValue = 0,
                    FromDateTime = DateTime.Today,
                    ToDateTime = DateTime.Today
                }
            },
                Dependencies = new List<LockTaskDependencyModel>()
            };

            Locks.Add(new LockEditorVm(model, _taskOptions));
        }

        public void RemoveLock(LockEditorVm vm)
        {
            Locks.Remove(vm);

            // Optional: renumber to keep it tidy
            var n = 1;
            foreach (var l in Locks.OrderBy(x => x.Model.LockNumber))
                l.Model.LockNumber = n++;
            foreach (var l in Locks)
                l.RefreshSummaries();
        }

        public List<LockModel> ToModels()
            => Locks.Select(x => x.Model).ToList();

    }

    internal sealed class LockEditorVm : ObservableObject
    {
        public LockModel Model { get; }

        public int LockNumber => Model.LockNumber;

        public string LockTitle => " "; // in mock-up it’s basically blank/readonly; number does the work

        public string ScheduleSummary => BuildScheduleSummary();
        public string TimeWindowSummary => BuildTimeWindowSummary();

        public ObservableCollection<DependencyRowVm> DependencyRows { get; } = new();

        public ObservableCollection<ScheduleRowVm> ScheduleRows { get; } = new();
        public List<DependencyTaskOption> TaskOptions { get; }

        public LockEditorVm(LockModel model, List<DependencyTaskOption> taskOptions)
        {
            Model = model;

            Model.Schedules ??= new List<LockScheduleModel>();
            Model.Dependencies ??= new List<LockTaskDependencyModel>();
            TaskOptions = taskOptions;

            RebuildScheduleRows();
            RebuildDependencyRows();
        }


        public void RebuildScheduleRows()
        {
            ScheduleRows.Clear();

            if (Model.Schedules == null) return;

            foreach (var s in Model.Schedules)
                ScheduleRows.Add(new ScheduleRowVm(this, s));

            RefreshSummaries();
        }

        public void RemoveSchedule(ScheduleRowVm scheduleVm)
        {
            if (Model.Schedules == null) return;

            Model.Schedules.Remove(scheduleVm.Model);
            RebuildScheduleRows();
        }


        public void RefreshSummaries()
        {
            RaisePropertyChanged(nameof(ScheduleSummary));
            RaisePropertyChanged(nameof(TimeWindowSummary));
        }

        public void RemoveDependency(DependencyRowVm depVm)
        {
            Model.Dependencies.Remove(depVm.Model);
            RebuildDependencyRows();
        }

        public void RebuildDependencyRows()
        {
            DependencyRows.Clear();

            if (Model.Dependencies != null)
            {
                foreach (var d in Model.Dependencies)
                    DependencyRows.Add(new DependencyRowVm(this, d, TaskOptions));
            }
        }

        private string BuildScheduleSummary()
        {
            if (Model.Schedules == null || Model.Schedules.Count == 0)
                return "None";

            if (Model.Schedules.Count == 1)
                return ScheduleRowVm.BuildSummary(Model.Schedules[0]);

            return $"{ScheduleRowVm.BuildSummary(Model.Schedules[0])} (+{Model.Schedules.Count - 1} more)";
        }

        private string BuildTimeWindowSummary()
        {
            // e.g. "2:30pm - 5:00pm"
            var start = Model.TimeWindowStart.ToString("h:mm", System.Globalization.CultureInfo.InvariantCulture)
                        + Model.TimeWindowStart.ToString("tt", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant();

            var end = Model.TimeWindowEnd.ToString("h:mm", System.Globalization.CultureInfo.InvariantCulture)
                      + Model.TimeWindowEnd.ToString("tt", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant();

            return $"{start} - {end}";
        }
    }

    internal sealed class DependencyRowVm
    {
        public LockEditorVm Owner { get; }
        public LockTaskDependencyModel Model { get; }
        public List<DependencyTaskOption> TaskOptions { get; }

        public string Summary => BuildSummary();

        public DependencyRowVm(LockEditorVm owner, LockTaskDependencyModel model, List<DependencyTaskOption> taskOptions)
        {
            Owner = owner;
            Model = model;
            TaskOptions = taskOptions;
        }

        private string BuildSummary()
        {
            var metricText = Model.MetricType == LockDependencyMetricType.ActiveTime
                ? $"{Model.TargetValue:0.#}h"
                : $"{Model.TargetValue:0.#}pts";

            var valenceText = Model.TargetValence == TargetValence.MustBeGreaterThan
                ? "≥"
                : "≤";

            var taskTitle = TaskOptions.Any(x => x.CardId == Model.TaskDependencyCardId)
                ? TaskOptions.First(x => x.CardId == Model.TaskDependencyCardId).Title
                : "";

            return $"{taskTitle}: {valenceText} {metricText} ({Model.TimeScope})";
        }
    }

    internal sealed class ScheduleRowVm
    {
        public LockEditorVm Owner { get; }
        public LockScheduleModel Model { get; }

        public string Summary => BuildSummary(Model);

        public ScheduleRowVm(LockEditorVm owner, LockScheduleModel model)
        {
            Owner = owner;
            Model = model;
        }

        public static string BuildSummary(LockScheduleModel s)
        {
            // Keep this aligned with ScheduleEditPage preview wording
            var t = s.FromDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            var start = s.FromDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = s.ToDateTime.HasValue
                ? s.ToDateTime.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "Never";

            var freq = s.FrequencyType switch
            {
                FrequencyType.Once => $"Once at {t}",
                FrequencyType.EveryDays => $"Every {Math.Max(1, s.FrequencyValue)} day(s) at {t}",
                FrequencyType.EveryWeekday => $"Every weekday at {t}",
                FrequencyType.EveryMonday => $"Every Monday at {t}",
                FrequencyType.EveryTuesday => $"Every Tuesday at {t}",
                FrequencyType.EveryWednesday => $"Every Wednesday at {t}",
                FrequencyType.EveryThursday => $"Every Thursday at {t}",
                FrequencyType.EveryFriday => $"Every Friday at {t}",
                FrequencyType.EverySaturday => $"Every Saturday at {t}",
                FrequencyType.EverySunday => $"Every Sunday at {t}",
                FrequencyType.EveryWeeks => $"Every {Math.Max(1, s.FrequencyValue)} week(s) at {t}",
                FrequencyType.EveryMonths => $"Every {Math.Max(1, s.FrequencyValue)} month(s) at {t}",
                FrequencyType.EveryYears => $"Every {Math.Max(1, s.FrequencyValue)} year(s) at {t}",
                _ => s.FrequencyType.ToString()
            };

            return $"{freq} · From: {start} · Ends: {end}";
        }
    }
}
