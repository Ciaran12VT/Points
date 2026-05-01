using Points.Models;
using Points.Services.Navigation;
using System.Globalization;

namespace Points.Views.Shared;

public partial class TaskDependencyEditPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly TaskCompletionSource<LockTaskDependencyModel> _tcs;
    private readonly List<DependencyTaskOption> _tasks;
    private readonly LockTaskDependencyModel _working;

    public Command DoneCommand { get; }

    public TaskDependencyEditPage(
        IEnumerable<DependencyTaskOption> tasks,
        LockTaskDependencyModel? initial,
        TaskCompletionSource<LockTaskDependencyModel> tcs,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        DoneCommand = new Command(async () => await DoneAsync());

        InitializeComponent();

        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _tcs = tcs;
        _tasks = tasks.ToList();

        // Work on a copy so Cancel/back doesn't mutate caller state
        _working = initial == null
            ? new LockTaskDependencyModel()
            : Clone(initial);

        // Populate pickers
        TaskPicker.ItemsSource = _tasks.Select(t => t.Title).ToList();

        MetricPicker.ItemsSource = new List<string> { "ActiveTime", "Points" };
        TimeScopePicker.ItemsSource = new List<string> { "Daily", "Weekly", "Monthly" };

        ValencePicker.ItemsSource = new List<string>
        {
            "Must Be Greater Than",
            "Must Be Less Than"
        };

        ValencePicker.SelectedIndex = _working.TargetValence == TargetValence.MustBeLessThan ? 1 : 0;

        // Initial selections
        if (_working.TaskDependencyCardId != 0)
        {
            var idx = _tasks.FindIndex(t => t.CardId == _working.TaskDependencyCardId);
            if (idx >= 0) TaskPicker.SelectedIndex = idx;
        }

        MetricPicker.SelectedIndex = _working.MetricType == LockDependencyMetricType.Points ? 1 : 0;
        TimeScopePicker.SelectedIndex = (int)_working.TimeScope;



        TargetEntry.Text = _working.TargetValue.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private async Task DoneAsync()
    {
        if (TaskPicker.SelectedIndex < 0)
        {
            await _dialogs.DisplayAlertAsync("Missing field", "Please select a Task.", "OK");
            return;
        }

        if (MetricPicker.SelectedIndex < 0)
        {
            await _dialogs.DisplayAlertAsync("Missing field", "Please select a Metric.", "OK");
            return;
        }

        if (TimeScopePicker.SelectedIndex < 0)
        {
            await _dialogs.DisplayAlertAsync("Missing field", "Please select a TimeScope.", "OK");
            return;
        }

        if (!double.TryParse(TargetEntry.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var target))
        {
            await _dialogs.DisplayAlertAsync("Invalid Target", "Target must be a number (e.g. 8 or 5.2).", "OK");
            return;
        }

        if (target <= 0)
        {
            await _dialogs.DisplayAlertAsync("Invalid Target", "Target must be greater than 0.", "OK");
            return;
        }

        if (ValencePicker.SelectedIndex < 0)
        {
            await _dialogs.DisplayAlertAsync("Missing field", "Please select a Condition.", "OK");
            return;
        }

        var selectedTask = _tasks[TaskPicker.SelectedIndex];

        _working.TaskDependencyCardId = selectedTask.CardId;
        _working.MetricType = MetricPicker.SelectedIndex == 1
            ? LockDependencyMetricType.Points
            : LockDependencyMetricType.ActiveTime;

        _working.TimeScope = (TimeScope)TimeScopePicker.SelectedIndex;
        _working.TargetValue = target;

        _working.TargetValence =
            ValencePicker.SelectedIndex == 1
                ? TargetValence.MustBeLessThan
                : TargetValence.MustBeGreaterThan;

        _tcs.TrySetResult(_working);
        await _navigation.PopAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetCanceled();
        return base.OnBackButtonPressed();
    }

    private static LockTaskDependencyModel Clone(LockTaskDependencyModel d) => new()
    {
        LockTaskDependencyId = d.LockTaskDependencyId,
        LockId = d.LockId,
        TaskDependencyCardId = d.TaskDependencyCardId,
        MetricType = d.MetricType,
        TimeScope = d.TimeScope,
        TargetValue = d.TargetValue,
        TargetValence = d.TargetValence,
    };
}

public sealed class DependencyTaskOption
{
    public long CardId { get; init; }
    public string Title { get; init; } = "";
}
