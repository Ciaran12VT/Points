using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using Points.Models;

namespace Points.ViewModels;

public sealed partial class LeaderboardViewModel
{
    private static readonly TimeSpan PlannerMatchTolerance = TimeSpan.FromMinutes(5);
    private static readonly double[] PlannerZoomLevels = { 0.6, 1.0, 1.75, 3.0, 5.0, 8.0 };

    private PlannerModel _planner = new() { PlannerDate = DateTime.Today };
    private PlannerDayData? _plannerDayData;
    private DateTime _plannerSelectedDate = DateTime.Today;
    private bool _isPlannerBusy;
    private string _plannerErrorMessage = "";
    private int _plannerZoomIndex = 1;
    private List<PlannerTaskCardOption> _plannerTaskOptions = new();
    private List<PlannerStepOption> _plannerStepOptions = new();
    private List<PlannerMissionOption> _plannerMissionOptions = new();

    public ObservableCollection<PlannerTimelineItemModel> PlannerTimelineItems { get; } = new();
    public ObservableCollection<PlannerTimeGuideModel> PlannerTimeGuides { get; } = new();

    public ICommand PlannerPreviousDateCommand { get; private set; } = null!;
    public ICommand PlannerNextDateCommand { get; private set; } = null!;
    public ICommand PlannerTodayCommand { get; private set; } = null!;
    public ICommand PlannerZoomInCommand { get; private set; } = null!;
    public ICommand PlannerZoomOutCommand { get; private set; } = null!;
    public ICommand PlannerZoomResetCommand { get; private set; } = null!;

    public DateTime PlannerSelectedDate
    {
        get => _plannerSelectedDate;
        set
        {
            var date = value.Date;
            if (_plannerSelectedDate == date) return;

            _plannerSelectedDate = date;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlannerSelectedDateText));
            _ = LoadPlannerAsync();
        }
    }

    public string PlannerSelectedDateText => PlannerSelectedDate.ToString("MMM-dd-yyyy", CultureInfo.CurrentCulture);

    public bool IsPlannerBusy
    {
        get => _isPlannerBusy;
        private set
        {
            if (_isPlannerBusy == value) return;
            _isPlannerBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNoPlannerTimelineItems));
        }
    }

    public string PlannerErrorMessage
    {
        get => _plannerErrorMessage;
        private set
        {
            if (_plannerErrorMessage == value) return;
            _plannerErrorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPlannerError));
            OnPropertyChanged(nameof(HasNoPlannerTimelineItems));
        }
    }

    public bool HasPlannerError => !string.IsNullOrWhiteSpace(PlannerErrorMessage);

    public bool HasNoPlannerTimelineItems =>
        !IsPlannerBusy && !HasPlannerError && PlannerTimelineItems.Count == 0;

    public double PlannerPixelsPerMinute => PlannerZoomLevels[_plannerZoomIndex];

    public double PlannerContentHeight => Math.Max(900, 1440 * PlannerPixelsPerMinute);

    public string PlannerZoomText => $"{PlannerPixelsPerMinute * 60:0} px/hr";

    public string PlannerSummaryText
    {
        get
        {
            var plannedTaskMinutes = _planner.Tasks.Sum(t => Math.Max(0, (t.PlannedEnd - t.PlannedStart).TotalMinutes));
            var plannedEventCount = _planner.Events.Sum(e => Math.Max(1, e.PlannedCount));
            var actualEventCount = BuildActualEventGroups().Sum(e => e.Count);

            return $"{PlannerSelectedDate:MMM-dd} | {_planner.Tasks.Count} tasks ({plannedTaskMinutes / 60:0.0}h) | {plannedEventCount} planned events | {actualEventCount} actual events";
        }
    }

    public IReadOnlyList<PlannerTaskCardOption> PlannerTaskOptions => _plannerTaskOptions;
    public IReadOnlyList<PlannerStepOption> PlannerStepOptions => _plannerStepOptions;
    public IReadOnlyList<PlannerMissionOption> PlannerMissionOptions => _plannerMissionOptions;

    private void InitializePlannerCommands()
    {
        PlannerPreviousDateCommand = new Command(() => PlannerSelectedDate = PlannerSelectedDate.AddDays(-1));
        PlannerNextDateCommand = new Command(() => PlannerSelectedDate = PlannerSelectedDate.AddDays(1));
        PlannerTodayCommand = new Command(() => PlannerSelectedDate = DateTime.Today);
        PlannerZoomInCommand = new Command(() => SetPlannerZoom(_plannerZoomIndex + 1));
        PlannerZoomOutCommand = new Command(() => SetPlannerZoom(_plannerZoomIndex - 1));
        PlannerZoomResetCommand = new Command(() => SetPlannerZoom(1));
    }

    private async Task LoadPlannerAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            PlannerErrorMessage = "";
            IsPlannerBusy = true;
        });

        try
        {
            var data = await _db.GetPlannerDayDataAsync(PlannerSelectedDate);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _plannerDayData = data;
                _planner = data.Planner ?? new PlannerModel { PlannerDate = PlannerSelectedDate };
                _planner.PlannerDate = PlannerSelectedDate;

                RebuildPlannerOptions();
                RebuildPlannerTimeline();
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PlannerTimelineItems.Clear();
                PlannerTimeGuides.Clear();
                PlannerErrorMessage = ex.Message;
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsPlannerBusy = false);
        }
    }

    public async Task UpsertPlannerTaskAsync(PlannerTaskModel task)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));

        task.PlannedStart = ClampToPlannerDate(task.PlannedStart);
        task.PlannedEnd = ClampToPlannerDate(task.PlannedEnd);

        var tasks = _planner.Tasks
            .Where(t => task.PlannerTaskId <= 0 || t.PlannerTaskId != task.PlannerTaskId)
            .Select(CloneTask)
            .ToList();

        tasks.Add(CloneTask(task));
        ValidatePlannedTaskSet(tasks);

        _planner.Tasks = tasks.OrderBy(t => t.PlannedStart).ToList();
        await SaveAndReloadPlannerAsync();
    }

    public async Task UpsertPlannerEventAsync(PlannerEventModel plannerEvent)
    {
        if (plannerEvent == null) throw new ArgumentNullException(nameof(plannerEvent));

        plannerEvent.PlannedTime = ClampToPlannerDate(plannerEvent.PlannedTime);
        plannerEvent.PlannedCount = Math.Max(1, plannerEvent.PlannedCount);

        var events = _planner.Events
            .Where(e => plannerEvent.PlannerEventId <= 0 || e.PlannerEventId != plannerEvent.PlannerEventId)
            .Select(CloneEvent)
            .ToList();

        events.Add(CloneEvent(plannerEvent));
        _planner.Events = events.OrderBy(e => e.PlannedTime).ToList();
        await SaveAndReloadPlannerAsync();
    }

    public async Task DeletePlannerItemAsync(PlannerTimelineItemModel item)
    {
        if (item.Task != null)
        {
            _planner.Tasks = _planner.Tasks
                .Where(t => t.PlannerTaskId != item.Task.PlannerTaskId)
                .ToList();
        }
        else if (item.Event != null)
        {
            _planner.Events = _planner.Events
                .Where(e => e.PlannerEventId != item.Event.PlannerEventId)
                .ToList();
        }

        await SaveAndReloadPlannerAsync();
    }

    private async Task SaveAndReloadPlannerAsync()
    {
        _planner.PlannerDate = PlannerSelectedDate;
        await _db.SavePlannerAsync(_planner);
        await LoadPlannerAsync();
    }

    private void SetPlannerZoom(int requestedIndex)
    {
        var index = Math.Clamp(requestedIndex, 0, PlannerZoomLevels.Length - 1);
        if (_plannerZoomIndex == index) return;

        _plannerZoomIndex = index;
        OnPropertyChanged(nameof(PlannerPixelsPerMinute));
        OnPropertyChanged(nameof(PlannerContentHeight));
        OnPropertyChanged(nameof(PlannerZoomText));
        RebuildPlannerTimeline();
    }

    private void RebuildPlannerOptions()
    {
        if (_plannerDayData == null)
        {
            _plannerTaskOptions = new List<PlannerTaskCardOption>();
            _plannerStepOptions = new List<PlannerStepOption>();
            _plannerMissionOptions = new List<PlannerMissionOption>();
            return;
        }

        _plannerTaskOptions = _plannerDayData.TaskCards
            .GroupBy(c => c.CardID)
            .Select(g =>
            {
                var card = g.First();
                var kind = GetCardKind(card);
                return new PlannerTaskCardOption(card.CardID, kind, card.Title);
            })
            .OrderBy(o => o.Title)
            .ToList();

        _plannerStepOptions = _plannerDayData.ScCards
            .SelectMany(card => card.Steps.Select(step => new PlannerStepOption(
                card.CardID,
                step.Id,
                card.Title,
                step.Title)))
            .OrderBy(o => o.DisplayTitle)
            .ToList();

        _plannerMissionOptions = _plannerDayData.MissionCards
            .GroupBy(m => m.CardID)
            .Select(g =>
            {
                var mission = g.First();
                return new PlannerMissionOption(mission.CardID, mission.Title);
            })
            .OrderBy(o => o.Title)
            .ToList();
    }

    private void RebuildPlannerTimeline()
    {
        PlannerTimeGuides.Clear();
        PlannerTimelineItems.Clear();

        BuildGuides();

        var actualTasks = BuildActualTaskSlices();
        var actualTaskStatuses = new Dictionary<int, PlannerMatchStatus>();

        foreach (var task in _planner.Tasks.OrderBy(t => t.PlannedStart))
        {
            var candidates = actualTasks
                .Where(a => a.CardId == task.CardId
                    && a.End > task.PlannedStart - PlannerMatchTolerance
                    && a.Start < task.PlannedEnd + PlannerMatchTolerance)
                .OrderBy(a => a.Start)
                .ToList();

            var status = PlannerMatchStatus.Missing;
            var subtitle = $"{task.PlannedStart:HH:mm}-{task.PlannedEnd:HH:mm}";

            if (candidates.Count > 0)
            {
                var earliest = candidates.Min(c => c.Start);
                var latest = candidates.Max(c => c.End);
                var plannedMinutes = (task.PlannedEnd - task.PlannedStart).TotalMinutes;
                var actualMinutes = candidates.Sum(c => (c.End - c.Start).TotalMinutes);

                var startOk = Abs(earliest - task.PlannedStart) <= PlannerMatchTolerance;
                var endOk = Abs(latest - task.PlannedEnd) <= PlannerMatchTolerance;
                var durationOk = Math.Abs(actualMinutes - plannedMinutes) <= 1;

                if (startOk && endOk)
                {
                    status = PlannerMatchStatus.FullMatch;
                    subtitle += $" | actual {earliest:HH:mm}-{latest:HH:mm}";
                    foreach (var candidate in candidates)
                        actualTaskStatuses[candidate.Id] = PlannerMatchStatus.FullMatch;
                }
                else if (durationOk)
                {
                    status = PlannerMatchStatus.PartialMatch;
                    subtitle += $" | {actualMinutes / 60:0.0}h actual";
                    foreach (var candidate in candidates)
                        actualTaskStatuses[candidate.Id] = PlannerMatchStatus.PartialMatch;
                }
            }

            PlannerTimelineItems.Add(CreateTimelineItem(
                lane: PlannerTimelineLane.Tasks,
                title: GetTaskCardTitle(task.CardId),
                subtitle: subtitle,
                start: task.PlannedStart,
                end: task.PlannedEnd,
                status: status,
                task: task,
                plannerEvent: null));
        }

        foreach (var actual in actualTasks)
        {
            var status = actualTaskStatuses.TryGetValue(actual.Id, out var actualStatus)
                ? actualStatus
                : PlannerMatchStatus.UnplannedActual;

            PlannerTimelineItems.Add(CreateTimelineItem(
                lane: PlannerTimelineLane.Tasks,
                title: actual.Title,
                subtitle: $"{actual.Start:HH:mm}-{actual.End:HH:mm}",
                start: actual.Start,
                end: actual.End,
                status: status,
                task: null,
                plannerEvent: null));
        }

        var actualEvents = BuildActualEventGroups();
        var actualEventStatuses = new Dictionary<int, PlannerMatchStatus>();

        foreach (var plannedEvent in _planner.Events.OrderBy(e => e.PlannedTime))
        {
            var candidates = actualEvents
                .Where(a => EventMatches(plannedEvent, a)
                    && !actualEventStatuses.ContainsKey(a.Id)
                    && Abs(a.Start - plannedEvent.PlannedTime) <= PlannerMatchTolerance)
                .OrderBy(a => Abs(a.Start - plannedEvent.PlannedTime))
                .ToList();

            var matched = candidates.FirstOrDefault();
            var status = PlannerMatchStatus.Missing;
            var title = GetPlannedEventTitle(plannedEvent);
            var subtitle = $"{plannedEvent.PlannedTime:HH:mm}";

            if (matched != null)
            {
                if (plannedEvent.EventKind == PlannerEventKind.ScStepRep)
                {
                    status = matched.Count == Math.Max(1, plannedEvent.PlannedCount)
                        ? PlannerMatchStatus.FullMatch
                        : PlannerMatchStatus.PartialMatch;
                    subtitle += $" | actual x{matched.Count}";
                }
                else
                {
                    status = PlannerMatchStatus.FullMatch;
                    subtitle += $" | actual {matched.Start:HH:mm}";
                }

                var delta = matched.Start - plannedEvent.PlannedTime;
                if (Math.Abs(delta.TotalMinutes) >= 1)
                    subtitle += $" ({delta.TotalMinutes:+0;-0}m)";

                actualEventStatuses[matched.Id] = status;
            }

            PlannerTimelineItems.Add(CreateTimelineItem(
                lane: PlannerTimelineLane.Events,
                title: title,
                subtitle: subtitle,
                start: plannedEvent.PlannedTime,
                end: plannedEvent.PlannedTime,
                status: status,
                task: null,
                plannerEvent: plannedEvent));
        }

        foreach (var actual in actualEvents)
        {
            var status = actualEventStatuses.TryGetValue(actual.Id, out var actualStatus)
                ? actualStatus
                : PlannerMatchStatus.UnplannedActual;

            PlannerTimelineItems.Add(CreateTimelineItem(
                lane: PlannerTimelineLane.Events,
                title: actual.Count > 1 ? $"{actual.Title} x{actual.Count}" : actual.Title,
                subtitle: actual.Count > 1
                    ? $"{actual.Start:HH:mm}-{actual.End:HH:mm} | x{actual.Count}"
                    : $"{actual.Start:HH:mm}",
                start: actual.Start,
                end: actual.End,
                status: status,
                task: null,
                plannerEvent: null));
        }

        SortTimelineItems();
        OnPropertyChanged(nameof(PlannerSummaryText));
        OnPropertyChanged(nameof(HasNoPlannerTimelineItems));
    }

    private void BuildGuides()
    {
        var interval = PlannerPixelsPerMinute >= 5 ? 5 :
            PlannerPixelsPerMinute >= 3 ? 15 :
            PlannerPixelsPerMinute >= 1.5 ? 30 :
            60;

        for (var minute = 0; minute <= 1440; minute += interval)
        {
            PlannerTimeGuides.Add(new PlannerTimeGuideModel
            {
                MinuteOfDay = minute,
                Top = minute * PlannerPixelsPerMinute,
                Label = minute % 60 == 0
                    ? TimeSpan.FromMinutes(minute).ToString(@"hh\:mm", CultureInfo.InvariantCulture)
                    : "",
                IsMajor = minute % 60 == 0
            });
        }
    }

    private PlannerTimelineItemModel CreateTimelineItem(
        PlannerTimelineLane lane,
        string title,
        string subtitle,
        DateTime start,
        DateTime end,
        PlannerMatchStatus status,
        PlannerTaskModel? task,
        PlannerEventModel? plannerEvent)
    {
        var dayStart = PlannerSelectedDate.Date;
        var top = Math.Max(0, (start - dayStart).TotalMinutes * PlannerPixelsPerMinute);
        var duration = Math.Max(0, (end - start).TotalMinutes);
        var height = Math.Max(28, duration * PlannerPixelsPerMinute);

        return new PlannerTimelineItemModel
        {
            Lane = lane,
            Title = title,
            Subtitle = subtitle,
            Start = start,
            End = end,
            Top = top,
            Height = height,
            Status = status,
            Task = task,
            Event = plannerEvent,
            BackgroundColor = GetStatusColor(status),
            TextColor = Colors.White
        };
    }

    private void SortTimelineItems()
    {
        var ordered = PlannerTimelineItems
            .OrderBy(i => i.Top)
            .ThenBy(i => i.Lane)
            .ToList();

        PlannerTimelineItems.Clear();
        foreach (var item in ordered)
            PlannerTimelineItems.Add(item);
    }

    private List<ActualTaskSlice> BuildActualTaskSlices()
    {
        if (_plannerDayData == null)
            return new List<ActualTaskSlice>();

        var result = new List<ActualTaskSlice>();
        var dayStart = PlannerSelectedDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var id = 1;

        foreach (var card in _plannerDayData.TaskCards)
        {
            foreach (var activity in card.Activity ?? Enumerable.Empty<ActivityModel>())
            {
                var actualEnd = activity.EndDate ?? DateTime.Now;
                var start = PlannerMax(activity.StartDate, dayStart);
                var end = PlannerMin(actualEnd, dayEnd);

                if (end <= start)
                    continue;

                result.Add(new ActualTaskSlice(
                    id++,
                    card.CardID,
                    GetCardKind(card),
                    card.Title,
                    start,
                    end));
            }
        }

        return result;
    }

    private List<ActualEventGroup> BuildActualEventGroups()
    {
        if (_plannerDayData == null)
            return new List<ActualEventGroup>();

        var dayStart = PlannerSelectedDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var atoms = new List<ActualEventAtom>();

        foreach (var card in _plannerDayData.ScCards)
        {
            foreach (var step in card.Steps)
            {
                foreach (var rep in step.Reps.Where(r => r >= dayStart && r < dayEnd))
                {
                    atoms.Add(new ActualEventAtom(
                        PlannerEventKind.ScStepRep,
                        card.CardID,
                        step.Id,
                        $"{card.Title}: {step.Title}",
                        rep));
                }
            }
        }

        foreach (var mission in _plannerDayData.MissionCards)
        {
            if (!mission.CompletedDate.HasValue)
                continue;

            var completedAt = mission.CompletedDate.Value;
            if (completedAt < dayStart || completedAt >= dayEnd)
                continue;

            atoms.Add(new ActualEventAtom(
                mission.IsFailed ? PlannerEventKind.MissionFail : PlannerEventKind.MissionComplete,
                mission.CardID,
                null,
                mission.Title,
                completedAt));
        }

        atoms = atoms.OrderBy(a => a.Time).ToList();

        var groups = new List<ActualEventGroup>();
        ActualEventGroup? current = null;
        var groupId = 1;

        foreach (var atom in atoms)
        {
            if (current != null && CanJoinEventGroup(current, atom))
            {
                current.Count++;
                current.End = atom.Time;
                current.LastTime = atom.Time;
                current.RepTimes.Add(atom.Time);
                continue;
            }

            if (current != null)
                groups.Add(current);

            current = new ActualEventGroup
            {
                Id = groupId++,
                Kind = atom.Kind,
                CardId = atom.CardId,
                ScCardStepId = atom.ScCardStepId,
                Title = atom.Title,
                Start = atom.Time,
                End = atom.Time,
                LastTime = atom.Time,
                Count = 1,
                RepTimes = atom.Kind == PlannerEventKind.ScStepRep
                    ? new List<DateTime> { atom.Time }
                    : new List<DateTime>()
            };
        }

        if (current != null)
            groups.Add(current);

        return groups;
    }

    private static bool CanJoinEventGroup(ActualEventGroup current, ActualEventAtom atom)
    {
        return current.Kind == PlannerEventKind.ScStepRep
            && atom.Kind == PlannerEventKind.ScStepRep
            && current.ScCardStepId == atom.ScCardStepId
            && atom.Time - current.LastTime <= PlannerMatchTolerance;
    }

    private static bool EventMatches(PlannerEventModel planned, ActualEventGroup actual)
    {
        if (planned.EventKind != actual.Kind)
            return false;

        if (planned.EventKind == PlannerEventKind.ScStepRep)
            return planned.ScCardStepId.HasValue && planned.ScCardStepId == actual.ScCardStepId;

        return planned.CardId == actual.CardId;
    }

    private string GetTaskCardTitle(long cardId) =>
        _plannerTaskOptions.FirstOrDefault(o => o.CardId == cardId)?.Title ?? $"Card {cardId}";

    private string GetPlannedEventTitle(PlannerEventModel plannerEvent)
    {
        return plannerEvent.EventKind switch
        {
            PlannerEventKind.ScStepRep => GetStepTitle(plannerEvent.ScCardStepId) + $" x{Math.Max(1, plannerEvent.PlannedCount)}",
            PlannerEventKind.MissionComplete => GetMissionTitle(plannerEvent.CardId) + " complete",
            PlannerEventKind.MissionFail => GetMissionTitle(plannerEvent.CardId) + " fail",
            _ => "Event"
        };
    }

    private string GetStepTitle(int? stepId)
    {
        if (!stepId.HasValue)
            return "Step";

        var option = _plannerStepOptions.FirstOrDefault(o => o.ScCardStepId == stepId.Value);
        return option?.DisplayTitle ?? $"Step {stepId.Value}";
    }

    private string GetMissionTitle(long cardId) =>
        _plannerMissionOptions.FirstOrDefault(o => o.CardId == cardId)?.Title ?? $"Mission {cardId}";

    private DateTime ClampToPlannerDate(DateTime value)
    {
        var dayStart = PlannerSelectedDate.Date;
        var dayEnd = dayStart.AddDays(1).AddMinutes(-1);
        var local = dayStart.Add(value.TimeOfDay);

        if (local < dayStart) return dayStart;
        if (local > dayEnd) return dayEnd;
        return local;
    }

    private static PlannerTaskModel CloneTask(PlannerTaskModel task) =>
        new()
        {
            PlannerTaskId = task.PlannerTaskId,
            PlannerId = task.PlannerId,
            CardId = task.CardId,
            CardKind = task.CardKind,
            PlannedStart = task.PlannedStart,
            PlannedEnd = task.PlannedEnd
        };

    private static PlannerEventModel CloneEvent(PlannerEventModel plannerEvent) =>
        new()
        {
            PlannerEventId = plannerEvent.PlannerEventId,
            PlannerId = plannerEvent.PlannerId,
            EventKind = plannerEvent.EventKind,
            CardId = plannerEvent.CardId,
            ScCardStepId = plannerEvent.ScCardStepId,
            PlannedTime = plannerEvent.PlannedTime,
            PlannedCount = plannerEvent.PlannedCount
        };

    private static void ValidatePlannedTaskSet(List<PlannerTaskModel> tasks)
    {
        var ordered = tasks.OrderBy(t => t.PlannedStart).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].PlannedEnd <= ordered[i].PlannedStart)
                throw new InvalidOperationException("Task end time must be after start time.");

            if (i > 0 && ordered[i].PlannedStart < ordered[i - 1].PlannedEnd)
                throw new InvalidOperationException("Planner tasks cannot overlap.");
        }
    }

    private static PlannerTaskCardKind GetCardKind(IActiveCardModel card)
    {
        return card switch
        {
            MissionCardModel => PlannerTaskCardKind.Mission,
            ScCardModel => PlannerTaskCardKind.ScCard,
            _ => PlannerTaskCardKind.TatCard
        };
    }

    private static Color GetStatusColor(PlannerMatchStatus status)
    {
        return status switch
        {
            PlannerMatchStatus.FullMatch => Color.FromArgb("#2E7D32"),
            PlannerMatchStatus.PartialMatch => Color.FromArgb("#EF8D32"),
            PlannerMatchStatus.Missing => Color.FromArgb("#B00020"),
            PlannerMatchStatus.UnplannedActual => Color.FromArgb("#1565C0"),
            _ => Color.FromArgb("#606060")
        };
    }

    private static TimeSpan Abs(TimeSpan value) =>
        value < TimeSpan.Zero ? value.Negate() : value;

    private static DateTime PlannerMax(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime PlannerMin(DateTime a, DateTime b) => a < b ? a : b;

    private sealed record ActualTaskSlice(
        int Id,
        long CardId,
        PlannerTaskCardKind Kind,
        string Title,
        DateTime Start,
        DateTime End);

    private sealed record ActualEventAtom(
        PlannerEventKind Kind,
        long CardId,
        int? ScCardStepId,
        string Title,
        DateTime Time);

    private sealed class ActualEventGroup
    {
        public int Id { get; init; }
        public PlannerEventKind Kind { get; init; }
        public long CardId { get; init; }
        public int? ScCardStepId { get; init; }
        public string Title { get; init; } = "";
        public DateTime Start { get; init; }
        public DateTime End { get; set; }
        public DateTime LastTime { get; set; }
        public int Count { get; set; }
        public List<DateTime> RepTimes { get; init; } = new();
    }
}

public sealed record PlannerTaskCardOption(
    long CardId,
    PlannerTaskCardKind Kind,
    string Title)
{
    public string DisplayTitle => $"{Title} ({Kind})";
}

public sealed record PlannerStepOption(
    long CardId,
    int ScCardStepId,
    string CardTitle,
    string StepTitle)
{
    public string DisplayTitle => $"{CardTitle}: {StepTitle}";
}

public sealed record PlannerMissionOption(long CardId, string Title);

public enum PlannerTimelineLane
{
    Tasks,
    Events
}

public sealed class PlannerTimelineItemModel
{
    public PlannerTimelineLane Lane { get; init; }
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public double Top { get; init; }
    public double Height { get; init; }
    public PlannerMatchStatus Status { get; init; }
    public PlannerTaskModel? Task { get; init; }
    public PlannerEventModel? Event { get; init; }
    public Color BackgroundColor { get; init; } = Colors.Gray;
    public Color TextColor { get; init; } = Colors.White;
    public bool IsPlanned => Task != null || Event != null;
}

public sealed class PlannerTimeGuideModel
{
    public int MinuteOfDay { get; init; }
    public double Top { get; init; }
    public string Label { get; init; } = "";
    public bool IsMajor { get; init; }
}
