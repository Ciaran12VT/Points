using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using Points.Models;

namespace Points.ViewModels.Leaderboard;

public sealed partial class LeaderboardViewModel
{
    private readonly LeaderboardPlannerController _plannerController;

    public ObservableCollection<PlannerTimelineItemModel> PlannerTimelineItems => _plannerController.TimelineItems;
    public ObservableCollection<PlannerTimeGuideModel> PlannerTimeGuides => _plannerController.TimeGuides;

    public ICommand PlannerPreviousDateCommand { get; private set; } = null!;
    public ICommand PlannerNextDateCommand { get; private set; } = null!;
    public ICommand PlannerTodayCommand { get; private set; } = null!;
    public ICommand PlannerZoomInCommand { get; private set; } = null!;
    public ICommand PlannerZoomOutCommand { get; private set; } = null!;
    public ICommand PlannerZoomResetCommand { get; private set; } = null!;

    public DateTime PlannerSelectedDate
    {
        get => _plannerController.SelectedDate;
        set
        {
            if (!_plannerController.SetSelectedDate(value))
                return;

            _ = LoadPlannerAsync();
        }
    }

    public string PlannerSelectedDateText => PlannerSelectedDate.ToString("MMM-dd-yyyy", CultureInfo.CurrentCulture);

    public bool IsPlannerBusy => _plannerController.IsBusy;

    public string PlannerErrorMessage => _plannerController.ErrorMessage;

    public bool HasPlannerError => _plannerController.HasError;

    public bool HasNoPlannerTimelineItems => _plannerController.HasNoTimelineItems;

    public double PlannerPixelsPerMinute => _plannerController.PixelsPerMinute;

    public double PlannerContentHeight => _plannerController.ContentHeight;

    public string PlannerZoomText => _plannerController.ZoomText;

    public string PlannerSummaryText => _plannerController.SummaryText;

    public IReadOnlyList<PlannerTaskCardOption> PlannerTaskOptions => _plannerController.TaskOptions;
    public IReadOnlyList<PlannerStepOption> PlannerStepOptions => _plannerController.StepOptions;
    public IReadOnlyList<PlannerMissionOption> PlannerMissionOptions => _plannerController.MissionOptions;

    private void InitializePlannerCommands()
    {
        PlannerPreviousDateCommand = new Command(() => PlannerSelectedDate = PlannerSelectedDate.AddDays(-1));
        PlannerNextDateCommand = new Command(() => PlannerSelectedDate = PlannerSelectedDate.AddDays(1));
        PlannerTodayCommand = new Command(() => PlannerSelectedDate = _plannerController.Today());
        PlannerZoomInCommand = new Command(_plannerController.ZoomIn);
        PlannerZoomOutCommand = new Command(_plannerController.ZoomOut);
        PlannerZoomResetCommand = new Command(_plannerController.ResetZoom);
    }

    private Task LoadPlannerAsync() => _plannerController.LoadAsync();

    public Task UpsertPlannerTaskAsync(PlannerTaskModel task) =>
        _plannerController.UpsertTaskAsync(task);

    public Task UpsertPlannerEventAsync(PlannerEventModel plannerEvent) =>
        _plannerController.UpsertEventAsync(plannerEvent);

    public Task DeletePlannerItemAsync(PlannerTimelineItemModel item) =>
        _plannerController.DeleteItemAsync(item);
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
