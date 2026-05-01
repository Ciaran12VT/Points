using System.Globalization;
using Microsoft.Maui.Graphics;
using Points.Models;

namespace Points.ViewModels.Leaderboard;

internal static class LeaderboardPlannerTimelineBuilder
{
    private static readonly TimeSpan MatchTolerance = TimeSpan.FromMinutes(5);

    public static LeaderboardPlannerTimelineBuildResult Build(LeaderboardPlannerTimelineBuildRequest request)
    {
        var timeGuides = BuildGuides(request.PixelsPerMinute).ToList();
        var timelineItems = new List<PlannerTimelineItemModel>();
        var actualTasks = BuildActualTaskSlices(request);
        var actualTaskStatuses = new Dictionary<int, PlannerMatchStatus>();

        foreach (var task in request.Planner.Tasks.OrderBy(t => t.PlannedStart))
        {
            var candidates = actualTasks
                .Where(a => a.CardId == task.CardId
                    && a.End > task.PlannedStart - MatchTolerance
                    && a.Start < task.PlannedEnd + MatchTolerance)
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

                var startOk = Abs(earliest - task.PlannedStart) <= MatchTolerance;
                var endOk = Abs(latest - task.PlannedEnd) <= MatchTolerance;
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

            timelineItems.Add(CreateTimelineItem(
                request,
                lane: PlannerTimelineLane.Tasks,
                title: GetTaskCardTitle(request, task.CardId),
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

            timelineItems.Add(CreateTimelineItem(
                request,
                lane: PlannerTimelineLane.Tasks,
                title: actual.Title,
                subtitle: $"{actual.Start:HH:mm}-{actual.End:HH:mm}",
                start: actual.Start,
                end: actual.End,
                status: status,
                task: null,
                plannerEvent: null));
        }

        var actualEvents = BuildActualEventGroups(request);
        var actualEventStatuses = new Dictionary<int, PlannerMatchStatus>();

        foreach (var plannedEvent in request.Planner.Events.OrderBy(e => e.PlannedTime))
        {
            var candidates = actualEvents
                .Where(a => EventMatches(plannedEvent, a)
                    && !actualEventStatuses.ContainsKey(a.Id)
                    && Abs(a.Start - plannedEvent.PlannedTime) <= MatchTolerance)
                .OrderBy(a => Abs(a.Start - plannedEvent.PlannedTime))
                .ToList();

            var matched = candidates.FirstOrDefault();
            var status = PlannerMatchStatus.Missing;
            var title = GetPlannedEventTitle(request, plannedEvent);
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

            timelineItems.Add(CreateTimelineItem(
                request,
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

            timelineItems.Add(CreateTimelineItem(
                request,
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

        var orderedItems = timelineItems
            .OrderBy(i => i.Top)
            .ThenBy(i => i.Lane)
            .ToList();

        return new LeaderboardPlannerTimelineBuildResult(
            timeGuides,
            orderedItems,
            actualEvents.Sum(e => e.Count));
    }

    private static IEnumerable<PlannerTimeGuideModel> BuildGuides(double pixelsPerMinute)
    {
        var interval = pixelsPerMinute >= 5 ? 5 :
            pixelsPerMinute >= 3 ? 15 :
            pixelsPerMinute >= 1.5 ? 30 :
            60;

        for (var minute = 0; minute <= 1440; minute += interval)
        {
            yield return new PlannerTimeGuideModel
            {
                MinuteOfDay = minute,
                Top = minute * pixelsPerMinute,
                Label = minute % 60 == 0
                    ? TimeSpan.FromMinutes(minute).ToString(@"hh\:mm", CultureInfo.InvariantCulture)
                    : "",
                IsMajor = minute % 60 == 0
            };
        }
    }

    private static PlannerTimelineItemModel CreateTimelineItem(
        LeaderboardPlannerTimelineBuildRequest request,
        PlannerTimelineLane lane,
        string title,
        string subtitle,
        DateTime start,
        DateTime end,
        PlannerMatchStatus status,
        PlannerTaskModel? task,
        PlannerEventModel? plannerEvent)
    {
        var dayStart = request.SelectedDate.Date;
        var top = Math.Max(0, (start - dayStart).TotalMinutes * request.PixelsPerMinute);
        var duration = Math.Max(0, (end - start).TotalMinutes);
        var height = Math.Max(28, duration * request.PixelsPerMinute);

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

    private static List<ActualTaskSlice> BuildActualTaskSlices(LeaderboardPlannerTimelineBuildRequest request)
    {
        if (request.DayData == null)
            return new List<ActualTaskSlice>();

        var result = new List<ActualTaskSlice>();
        var dayStart = request.SelectedDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var id = 1;

        foreach (var card in request.DayData.TaskCards)
        {
            foreach (var activity in card.Activity ?? Enumerable.Empty<ActivityModel>())
            {
                var actualStart = request.ToLocalWallClock(activity.StartDate);
                var actualEnd = activity.EndDate.HasValue
                    ? request.ToLocalWallClock(activity.EndDate.Value)
                    : request.LocalNow();
                var start = Max(actualStart, dayStart);
                var end = Min(actualEnd, dayEnd);

                if (end <= start)
                    continue;

                result.Add(new ActualTaskSlice(
                    id++,
                    card.CardID,
                    card.Title,
                    start,
                    end));
            }
        }

        return result;
    }

    private static List<ActualEventGroup> BuildActualEventGroups(LeaderboardPlannerTimelineBuildRequest request)
    {
        if (request.DayData == null)
            return new List<ActualEventGroup>();

        var dayStart = request.SelectedDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var atoms = new List<ActualEventAtom>();

        foreach (var card in request.DayData.ScCards)
        {
            foreach (var step in card.Steps)
            {
                foreach (var rep in step.Reps.Select(request.ToLocalWallClock).Where(r => r >= dayStart && r < dayEnd))
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

        foreach (var mission in request.DayData.MissionCards)
        {
            if (!mission.CompletedDate.HasValue)
                continue;

            var completedAt = request.ToLocalWallClock(mission.CompletedDate.Value);
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
            && atom.Time - current.LastTime <= MatchTolerance;
    }

    private static bool EventMatches(PlannerEventModel planned, ActualEventGroup actual)
    {
        if (planned.EventKind != actual.Kind)
            return false;

        if (planned.EventKind == PlannerEventKind.ScStepRep)
            return planned.ScCardStepId.HasValue && planned.ScCardStepId == actual.ScCardStepId;

        return planned.CardId == actual.CardId;
    }

    private static string GetTaskCardTitle(LeaderboardPlannerTimelineBuildRequest request, long cardId) =>
        request.TaskOptions.FirstOrDefault(o => o.CardId == cardId)?.Title ?? $"Card {cardId}";

    private static string GetPlannedEventTitle(
        LeaderboardPlannerTimelineBuildRequest request,
        PlannerEventModel plannerEvent)
    {
        return plannerEvent.EventKind switch
        {
            PlannerEventKind.ScStepRep => GetStepTitle(request, plannerEvent.ScCardStepId) + $" x{Math.Max(1, plannerEvent.PlannedCount)}",
            PlannerEventKind.MissionComplete => GetMissionTitle(request, plannerEvent.CardId) + " complete",
            PlannerEventKind.MissionFail => GetMissionTitle(request, plannerEvent.CardId) + " fail",
            _ => "Event"
        };
    }

    private static string GetStepTitle(LeaderboardPlannerTimelineBuildRequest request, int? stepId)
    {
        if (!stepId.HasValue)
            return "Step";

        var option = request.StepOptions.FirstOrDefault(o => o.ScCardStepId == stepId.Value);
        return option?.DisplayTitle ?? $"Step {stepId.Value}";
    }

    private static string GetMissionTitle(LeaderboardPlannerTimelineBuildRequest request, long cardId) =>
        request.MissionOptions.FirstOrDefault(o => o.CardId == cardId)?.Title ?? $"Mission {cardId}";

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

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private sealed record ActualTaskSlice(
        int Id,
        long CardId,
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

internal sealed record LeaderboardPlannerTimelineBuildRequest(
    PlannerModel Planner,
    PlannerDayData? DayData,
    DateTime SelectedDate,
    double PixelsPerMinute,
    IReadOnlyList<PlannerTaskCardOption> TaskOptions,
    IReadOnlyList<PlannerStepOption> StepOptions,
    IReadOnlyList<PlannerMissionOption> MissionOptions,
    Func<DateTime> LocalNow,
    Func<DateTime, DateTime> ToLocalWallClock);

internal sealed record LeaderboardPlannerTimelineBuildResult(
    IReadOnlyList<PlannerTimeGuideModel> TimeGuides,
    IReadOnlyList<PlannerTimelineItemModel> TimelineItems,
    int ActualEventCount);
