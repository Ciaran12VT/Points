using Points.Models;
using Points.Services.Navigation;
using Points.ViewModels.Leaderboard;
using System.Globalization;

namespace Points.Views.Leaderboard;

internal sealed class LeaderboardPlannerInteractionCoordinator
{
    private readonly LeaderboardViewModel _viewModel;
    private readonly IAppDialogService _dialogs;

    public LeaderboardPlannerInteractionCoordinator(
        LeaderboardViewModel viewModel,
        IAppDialogService dialogs)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public async Task AddPlannerTaskAsync()
    {
        var task = await PromptPlannerTaskAsync(null);
        if (task == null) return;

        try
        {
            await _viewModel.UpsertPlannerTaskAsync(task);
        }
        catch (Exception ex)
        {
            await ShowPlannerAlertAsync("Task not saved", ex.Message);
        }
    }

    public async Task AddPlannerEventAsync()
    {
        var plannerEvent = await PromptPlannerEventAsync(null);
        if (plannerEvent == null) return;

        try
        {
            await _viewModel.UpsertPlannerEventAsync(plannerEvent);
        }
        catch (Exception ex)
        {
            await ShowPlannerAlertAsync("Event not saved", ex.Message);
        }
    }

    public async Task OnPlannerTimelineItemTappedAsync(PlannerTimelineItemModel item)
    {
        if (!item.IsPlanned)
        {
            await _dialogs.DisplayAlertAsync(item.Title, item.Subtitle, "OK");
            return;
        }

        var action = await _dialogs.DisplayActionSheetAsync(item.Title, "Cancel", "Delete", "Edit");
        if (action == "Delete")
        {
            var confirm = await _dialogs.DisplayAlertAsync("Delete item?", item.Title, "Delete", "Cancel");
            if (!confirm) return;

            try
            {
                await _viewModel.DeletePlannerItemAsync(item);
            }
            catch (Exception ex)
            {
                await ShowPlannerAlertAsync("Item not deleted", ex.Message);
            }

            return;
        }

        if (action != "Edit")
            return;

        try
        {
            if (item.Task != null)
            {
                var edited = await PromptPlannerTaskAsync(item.Task);
                if (edited != null)
                    await _viewModel.UpsertPlannerTaskAsync(edited);
            }
            else if (item.Event != null)
            {
                var edited = await PromptPlannerEventAsync(item.Event);
                if (edited != null)
                    await _viewModel.UpsertPlannerEventAsync(edited);
            }
        }
        catch (Exception ex)
        {
            await ShowPlannerAlertAsync("Item not saved", ex.Message);
        }
    }

    private async Task<PlannerTaskModel?> PromptPlannerTaskAsync(PlannerTaskModel? existing)
    {
        var options = _viewModel.PlannerTaskOptions.ToList();
        if (options.Count == 0)
        {
            await _dialogs.DisplayAlertAsync("No task cards", "There are no task cards available for this date.", "OK");
            return null;
        }

        var selected = await PickTaskOptionAsync(options);
        if (selected == null)
            return null;

        var defaultStart = existing?.PlannedStart ?? _viewModel.PlannerSelectedDate.Date.AddHours(9);
        var start = await PromptTimeAsync("Task start", defaultStart);
        if (!start.HasValue)
            return null;

        var defaultEnd = existing?.PlannedEnd ?? start.Value.AddHours(1);
        var end = await PromptTimeAsync("Task end", defaultEnd);
        if (!end.HasValue)
            return null;

        return new PlannerTaskModel
        {
            PlannerTaskId = existing?.PlannerTaskId ?? 0,
            PlannerId = existing?.PlannerId ?? 0,
            CardId = selected.CardId,
            CardKind = selected.Kind,
            PlannedStart = start.Value,
            PlannedEnd = end.Value
        };
    }

    private async Task<PlannerEventModel?> PromptPlannerEventAsync(PlannerEventModel? existing)
    {
        var kind = existing?.EventKind;
        if (!kind.HasValue)
        {
            var kindChoice = await _dialogs.DisplayActionSheetAsync(
                "Event type",
                "Cancel",
                null,
                "SC step reps",
                "Mission complete",
                "Mission fail");

            kind = kindChoice switch
            {
                "SC step reps" => PlannerEventKind.ScStepRep,
                "Mission complete" => PlannerEventKind.MissionComplete,
                "Mission fail" => PlannerEventKind.MissionFail,
                _ => null
            };
        }

        if (!kind.HasValue)
            return null;

        var plannedTime = await PromptTimeAsync(
            "Event time",
            existing?.PlannedTime ?? _viewModel.PlannerSelectedDate.Date.AddHours(9));

        if (!plannedTime.HasValue)
            return null;

        if (kind == PlannerEventKind.ScStepRep)
        {
            var stepOptions = _viewModel.PlannerStepOptions.ToList();
            if (stepOptions.Count == 0)
            {
                await _dialogs.DisplayAlertAsync("No SC steps", "There are no SC steps available for this date.", "OK");
                return null;
            }

            var step = await PickStepOptionAsync(stepOptions);
            if (step == null)
                return null;

            var count = await PromptCountAsync(existing?.PlannedCount ?? 1);
            if (!count.HasValue)
                return null;

            return new PlannerEventModel
            {
                PlannerEventId = existing?.PlannerEventId ?? 0,
                PlannerId = existing?.PlannerId ?? 0,
                EventKind = PlannerEventKind.ScStepRep,
                CardId = step.CardId,
                ScCardStepId = step.ScCardStepId,
                PlannedTime = plannedTime.Value,
                PlannedCount = count.Value
            };
        }

        var missionOptions = _viewModel.PlannerMissionOptions.ToList();
        if (missionOptions.Count == 0)
        {
            await _dialogs.DisplayAlertAsync("No missions", "There are no missions available for this date.", "OK");
            return null;
        }

        var mission = await PickMissionOptionAsync(missionOptions);
        if (mission == null)
            return null;

        return new PlannerEventModel
        {
            PlannerEventId = existing?.PlannerEventId ?? 0,
            PlannerId = existing?.PlannerId ?? 0,
            EventKind = kind.Value,
            CardId = mission.CardId,
            ScCardStepId = null,
            PlannedTime = plannedTime.Value,
            PlannedCount = 1
        };
    }

    private async Task<PlannerTaskCardOption?> PickTaskOptionAsync(List<PlannerTaskCardOption> options)
    {
        var labels = options.Select(o => o.DisplayTitle).ToList();
        var choice = await _dialogs.DisplayActionSheetAsync("Task card", "Cancel", null, labels.ToArray());

        return choice == null || choice == "Cancel"
            ? null
            : options.FirstOrDefault(o => o.DisplayTitle == choice);
    }

    private async Task<PlannerStepOption?> PickStepOptionAsync(List<PlannerStepOption> options)
    {
        var labels = options.Select(o => o.DisplayTitle).ToList();
        var choice = await _dialogs.DisplayActionSheetAsync("SC step", "Cancel", null, labels.ToArray());

        return choice == null || choice == "Cancel"
            ? null
            : options.FirstOrDefault(o => o.DisplayTitle == choice);
    }

    private async Task<PlannerMissionOption?> PickMissionOptionAsync(List<PlannerMissionOption> options)
    {
        var labels = options.Select(o => o.Title).ToList();
        var choice = await _dialogs.DisplayActionSheetAsync("Mission", "Cancel", null, labels.ToArray());

        return choice == null || choice == "Cancel"
            ? null
            : options.FirstOrDefault(o => o.Title == choice);
    }

    private async Task<DateTime?> PromptTimeAsync(string title, DateTime initial)
    {
        var input = await _dialogs.DisplayPromptAsync(
            title,
            "Enter time as HH:mm",
            accept: "OK",
            cancel: "Cancel",
            initialValue: initial.ToString("HH:mm", CultureInfo.InvariantCulture),
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (!TryParsePlannerTime(input, out var time))
        {
            await _dialogs.DisplayAlertAsync("Invalid time", "Use HH:mm, for example 09:30.", "OK");
            return null;
        }

        return _viewModel.PlannerSelectedDate.Date.Add(time);
    }

    private async Task<int?> PromptCountAsync(int initial)
    {
        var input = await _dialogs.DisplayPromptAsync(
            "Rep count",
            "Enter planned rep count",
            accept: "OK",
            cancel: "Cancel",
            initialValue: Math.Max(1, initial).ToString(CultureInfo.InvariantCulture),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            && !int.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out count))
        {
            await _dialogs.DisplayAlertAsync("Invalid count", "Enter a whole number greater than zero.", "OK");
            return null;
        }

        return Math.Max(1, count);
    }

    private static bool TryParsePlannerTime(string input, out TimeSpan time)
    {
        input = input.Trim();

        return TimeSpan.TryParseExact(input, @"hh\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParseExact(input, @"h\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParse(input, CultureInfo.CurrentCulture, out time);
    }

    private Task ShowPlannerAlertAsync(string title, string message)
    {
        return _dialogs.DisplayAlertAsync(title, message, "OK");
    }
}
