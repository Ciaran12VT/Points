using Points.Models;
using Points.ViewModels;
using System.Globalization;

namespace Points.Views.Shared;

public partial class TaskDependencyEditPage : ContentPage
{
    private readonly TaskCompletionSource<LockTaskDependencyModel> _tcs;
    private readonly List<DependencyTaskOption> _tasks;
    private readonly LockTaskDependencyModel _working;

    public TaskDependencyEditPage(
        IEnumerable<DependencyTaskOption> tasks,
        LockTaskDependencyModel? initial,
        TaskCompletionSource<LockTaskDependencyModel> tcs)
    {
        InitializeComponent();

        _tcs = tcs;
        _tasks = tasks.ToList();

        // Work on a copy so Cancel/back doesn’t mutate caller state
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

        ValencePicker.SelectedIndex = _working.GoalValence == GoalValence.MustBeLessThan ? 1 : 0;

        // Initial selections
        if (_working.TaskDependencyCardId != 0)
        {
            var idx = _tasks.FindIndex(t => t.CardId == _working.TaskDependencyCardId);
            if (idx >= 0) TaskPicker.SelectedIndex = idx;
        }

        MetricPicker.SelectedIndex = _working.MetricType == LockDependencyMetricType.Points ? 1 : 0;
        TimeScopePicker.SelectedIndex = (int)_working.TimeScope;



        GoalEntry.Text = _working.GoalValue.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        if (TaskPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Missing field", "Please select a Task.", "OK");
            return;
        }

        if (MetricPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Missing field", "Please select a Metric.", "OK");
            return;
        }

        if (TimeScopePicker.SelectedIndex < 0)
        {
            await DisplayAlert("Missing field", "Please select a TimeScope.", "OK");
            return;
        }

        if (!double.TryParse(GoalEntry.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var goal))
        {
            await DisplayAlert("Invalid Goal", "Goal must be a number (e.g. 8 or 5.2).", "OK");
            return;
        }

        if (goal <= 0)
        {
            await DisplayAlert("Invalid Goal", "Goal must be greater than 0.", "OK");
            return;
        }

        if (ValencePicker.SelectedIndex < 0)
        {
            await DisplayAlert("Missing field", "Please select a Condition.", "OK");
            return;
        }

        var selectedTask = _tasks[TaskPicker.SelectedIndex];

        _working.TaskDependencyCardId = selectedTask.CardId;
        _working.MetricType = MetricPicker.SelectedIndex == 1
            ? LockDependencyMetricType.Points
            : LockDependencyMetricType.ActiveTime;

        _working.TimeScope = (TimeScope)TimeScopePicker.SelectedIndex;
        _working.GoalValue = goal;

        _working.GoalValence =
            ValencePicker.SelectedIndex == 1
                ? GoalValence.MustBeLessThan
                : GoalValence.MustBeGreaterThan;

        _tcs.TrySetResult(_working);
        await Navigation.PopAsync();
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
        GoalValue = d.GoalValue,
        GoalValence = d.GoalValence,
    };
}

public sealed class DependencyTaskOption
{
    public long CardId { get; init; }
    public string Title { get; init; } = "";
}