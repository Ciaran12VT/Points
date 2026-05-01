using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Points.Models;
using Points.Services.Persistence;

namespace Points.ViewModels.Leaderboard;

internal sealed class LeaderboardPlannerController
{
    private static readonly double[] ZoomLevels = { 0.6, 1.0, 1.75, 3.0, 5.0, 8.0 };

    private readonly IPlannerService _plannerService;
    private readonly Func<DateTime> _localNow;
    private readonly Func<DateTime, DateTime> _toLocalWallClock;
    private readonly Action<string?> _notify;

    private PlannerModel _planner = new();
    private PlannerDayData? _dayData;
    private LeaderboardPlannerOptions _options = LeaderboardPlannerOptions.Empty;
    private DateTime _selectedDate = DateTime.MinValue;
    private bool _isBusy;
    private string _errorMessage = "";
    private int _zoomIndex = 1;
    private int _actualEventCount;

    public LeaderboardPlannerController(
        IPlannerService plannerService,
        Func<DateTime> localNow,
        Func<DateTime, DateTime> toLocalWallClock,
        Action<string?> notify)
    {
        _plannerService = plannerService ?? throw new ArgumentNullException(nameof(plannerService));
        _localNow = localNow ?? throw new ArgumentNullException(nameof(localNow));
        _toLocalWallClock = toLocalWallClock ?? throw new ArgumentNullException(nameof(toLocalWallClock));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));

        var today = Today();
        _selectedDate = today;
        _planner = new PlannerModel { PlannerDate = today };
    }

    public ObservableCollection<PlannerTimelineItemModel> TimelineItems { get; } = new();
    public ObservableCollection<PlannerTimeGuideModel> TimeGuides { get; } = new();

    public DateTime SelectedDate => _selectedDate;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            Notify(nameof(LeaderboardViewModel.IsPlannerBusy));
            Notify(nameof(LeaderboardViewModel.HasNoPlannerTimelineItems));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            Notify(nameof(LeaderboardViewModel.PlannerErrorMessage));
            Notify(nameof(LeaderboardViewModel.HasPlannerError));
            Notify(nameof(LeaderboardViewModel.HasNoPlannerTimelineItems));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoTimelineItems => !IsBusy && !HasError && TimelineItems.Count == 0;

    public double PixelsPerMinute => ZoomLevels[_zoomIndex];

    public double ContentHeight => Math.Max(900, 1440 * PixelsPerMinute);

    public string ZoomText => $"{PixelsPerMinute * 60:0} px/hr";

    public string SummaryText
    {
        get
        {
            var plannedTaskMinutes = _planner.Tasks.Sum(t => Math.Max(0, (t.PlannedEnd - t.PlannedStart).TotalMinutes));
            var plannedEventCount = _planner.Events.Sum(e => Math.Max(1, e.PlannedCount));

            return $"{SelectedDate:MMM-dd} | {_planner.Tasks.Count} tasks ({plannedTaskMinutes / 60:0.0}h) | {plannedEventCount} planned events | {_actualEventCount} actual events";
        }
    }

    public IReadOnlyList<PlannerTaskCardOption> TaskOptions => _options.TaskOptions;
    public IReadOnlyList<PlannerStepOption> StepOptions => _options.StepOptions;
    public IReadOnlyList<PlannerMissionOption> MissionOptions => _options.MissionOptions;

    public DateTime Today() => _localNow().Date;

    public bool SetSelectedDate(DateTime value)
    {
        var date = ToLocalWallClock(value).Date;
        if (_selectedDate == date)
            return false;

        _selectedDate = date;
        Notify(nameof(LeaderboardViewModel.PlannerSelectedDate));
        Notify(nameof(LeaderboardViewModel.PlannerSelectedDateText));
        return true;
    }

    public async Task LoadAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ErrorMessage = "";
            IsBusy = true;
        });

        try
        {
            var data = await _plannerService.GetPlannerDayDataAsync(SelectedDate);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _dayData = data;
                _planner = data.Planner ?? new PlannerModel { PlannerDate = SelectedDate };
                _planner.PlannerDate = SelectedDate;

                RebuildOptions();
                RebuildTimeline();
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                TimelineItems.Clear();
                TimeGuides.Clear();
                _actualEventCount = 0;
                ErrorMessage = ex.Message;
                Notify(nameof(LeaderboardViewModel.PlannerSummaryText));
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    public async Task UpsertTaskAsync(PlannerTaskModel task)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));

        task.PlannedStart = ClampToSelectedDate(task.PlannedStart);
        task.PlannedEnd = ClampToSelectedDate(task.PlannedEnd);

        var tasks = _planner.Tasks
            .Where(t => task.PlannerTaskId <= 0 || t.PlannerTaskId != task.PlannerTaskId)
            .Select(CloneTask)
            .ToList();

        tasks.Add(CloneTask(task));
        ValidatePlannedTaskSet(tasks);

        _planner.Tasks = tasks.OrderBy(t => t.PlannedStart).ToList();
        await SaveAndReloadAsync();
    }

    public async Task UpsertEventAsync(PlannerEventModel plannerEvent)
    {
        if (plannerEvent == null) throw new ArgumentNullException(nameof(plannerEvent));

        plannerEvent.PlannedTime = ClampToSelectedDate(plannerEvent.PlannedTime);
        plannerEvent.PlannedCount = Math.Max(1, plannerEvent.PlannedCount);

        var events = _planner.Events
            .Where(e => plannerEvent.PlannerEventId <= 0 || e.PlannerEventId != plannerEvent.PlannerEventId)
            .Select(CloneEvent)
            .ToList();

        events.Add(CloneEvent(plannerEvent));
        _planner.Events = events.OrderBy(e => e.PlannedTime).ToList();
        await SaveAndReloadAsync();
    }

    public async Task DeleteItemAsync(PlannerTimelineItemModel item)
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

        await SaveAndReloadAsync();
    }

    public void ZoomIn() => SetZoom(_zoomIndex + 1);

    public void ZoomOut() => SetZoom(_zoomIndex - 1);

    public void ResetZoom() => SetZoom(1);

    private async Task SaveAndReloadAsync()
    {
        _planner.PlannerDate = SelectedDate;
        await _plannerService.SavePlannerAsync(_planner);
        await LoadAsync();
    }

    private void SetZoom(int requestedIndex)
    {
        var index = Math.Clamp(requestedIndex, 0, ZoomLevels.Length - 1);
        if (_zoomIndex == index)
            return;

        _zoomIndex = index;
        Notify(nameof(LeaderboardViewModel.PlannerPixelsPerMinute));
        Notify(nameof(LeaderboardViewModel.PlannerContentHeight));
        Notify(nameof(LeaderboardViewModel.PlannerZoomText));
        RebuildTimeline();
    }

    private void RebuildOptions()
    {
        _options = LeaderboardPlannerOptionBuilder.Build(_dayData);
        Notify(nameof(LeaderboardViewModel.PlannerTaskOptions));
        Notify(nameof(LeaderboardViewModel.PlannerStepOptions));
        Notify(nameof(LeaderboardViewModel.PlannerMissionOptions));
    }

    private void RebuildTimeline()
    {
        var result = LeaderboardPlannerTimelineBuilder.Build(new LeaderboardPlannerTimelineBuildRequest(
            _planner,
            _dayData,
            SelectedDate,
            PixelsPerMinute,
            _options.TaskOptions,
            _options.StepOptions,
            _options.MissionOptions,
            _localNow,
            ToLocalWallClock));

        ReplaceCollection(TimeGuides, result.TimeGuides);
        ReplaceCollection(TimelineItems, result.TimelineItems);
        _actualEventCount = result.ActualEventCount;

        Notify(nameof(LeaderboardViewModel.PlannerSummaryText));
        Notify(nameof(LeaderboardViewModel.HasNoPlannerTimelineItems));
    }

    private DateTime ClampToSelectedDate(DateTime value)
    {
        var dayStart = SelectedDate.Date;
        var dayEnd = dayStart.AddDays(1).AddMinutes(-1);
        var local = dayStart.Add(ToLocalWallClock(value).TimeOfDay);

        if (local < dayStart) return dayStart;
        if (local > dayEnd) return dayEnd;
        return new DateTime(local.Ticks, DateTimeKind.Unspecified);
    }

    private DateTime ToLocalWallClock(DateTime value) => _toLocalWallClock(value);

    private void Notify(string propertyName) => _notify(propertyName);

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
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
}
