using System.Collections.ObjectModel;
using System.Globalization;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Scheduling;
using Points.Services.Time;
using Points.Views.Schedules;
using Points.Views.Shared;

namespace Points.ViewModels.Shared;

internal sealed class EditLocksVm
{
    private readonly long _cardId;
    private readonly List<DependencyTaskOption> _taskOptions;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly ILockService _locks;
    private readonly IClock _clock;
    private readonly List<LockModel> _targetLocks;
    private readonly Action _onChanged;

    public ObservableCollection<LockEditorVm> Locks { get; } = new();

    public Command AddLockCommand { get; }
    public Command<LockEditorVm> RemoveLockCommand { get; }
    public Command<LockEditorVm> AddScheduleCommand { get; }
    public Command<ScheduleRowVm> EditScheduleCommand { get; }
    public Command<ScheduleRowVm> RemoveScheduleCommand { get; }
    public Command<LockEditorVm> EditTimeWindowCommand { get; }
    public Command<LockEditorVm> AddDependencyCommand { get; }
    public Command<DependencyRowVm> EditDependencyCommand { get; }
    public Command<DependencyRowVm> RemoveDependencyCommand { get; }
    public Command SaveCommand { get; }
    public Command CancelCommand { get; }

    public EditLocksVm(
        long cardId,
        List<LockModel> sourceLocks,
        ILockService locks,
        List<DependencyTaskOption> taskOptions,
        List<LockModel> targetLocks,
        Action onChanged,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
    {
        _cardId = cardId;
        _locks = locks ?? throw new ArgumentNullException(nameof(locks));
        _taskOptions = taskOptions ?? throw new ArgumentNullException(nameof(taskOptions));
        _targetLocks = targetLocks ?? throw new ArgumentNullException(nameof(targetLocks));
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        AddLockCommand = new Command(AddLock);
        RemoveLockCommand = new Command<LockEditorVm>(async lockVm => await RemoveLockAsync(lockVm));
        AddScheduleCommand = new Command<LockEditorVm>(async lockVm => await AddScheduleAsync(lockVm));
        EditScheduleCommand = new Command<ScheduleRowVm>(async scheduleVm => await EditScheduleAsync(scheduleVm));
        RemoveScheduleCommand = new Command<ScheduleRowVm>(RemoveSchedule);
        EditTimeWindowCommand = new Command<LockEditorVm>(async lockVm => await EditTimeWindowAsync(lockVm));
        AddDependencyCommand = new Command<LockEditorVm>(async lockVm => await AddDependencyAsync(lockVm));
        EditDependencyCommand = new Command<DependencyRowVm>(async depVm => await EditDependencyAsync(depVm));
        RemoveDependencyCommand = new Command<DependencyRowVm>(RemoveDependency);
        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await _navigation.PopAsync());

        foreach (var lockModel in CloneLocks(sourceLocks).OrderBy(x => x.LockNumber))
            Locks.Add(new LockEditorVm(lockModel, _taskOptions));
    }

    public void NotifyChanged()
    {
        _onChanged.Invoke();
    }

    private void AddLock()
    {
        var next = Locks.Count == 0 ? 1 : Locks.Max(x => x.Model.LockNumber) + 1;
        var today = WallClockScheduleTime.NormalizeLocal(_clock.LocalNow).Date;

        var model = new LockModel
        {
            CardId = _cardId,
            LockNumber = next,
            TimeWindowStart = new TimeOnly(0, 0, 0),
            TimeWindowEnd = new TimeOnly(23, 59, 59),
            Schedules = new List<LockScheduleModel>
            {
                new()
                {
                    FrequencyType = FrequencyType.Once,
                    FrequencyValue = 0,
                    FromDateTime = today,
                    ToDateTime = today
                }
            },
            Dependencies = new List<LockTaskDependencyModel>()
        };

        Locks.Add(new LockEditorVm(model, _taskOptions));
    }

    private async Task RemoveLockAsync(LockEditorVm? lockVm)
    {
        if (lockVm == null)
            return;

        await _locks.DeleteLockModelAsync(lockVm.Model);
        Locks.Remove(lockVm);

        var next = 1;
        foreach (var lockEditor in Locks.OrderBy(x => x.Model.LockNumber))
            lockEditor.Model.LockNumber = next++;

        foreach (var lockEditor in Locks)
            lockEditor.RefreshSummaries();
    }

    private async Task EditScheduleAsync(ScheduleRowVm? scheduleVm)
    {
        if (scheduleVm == null)
            return;

        var saved = await OpenScheduleEditorAsync(scheduleVm.Model);
        if (!saved)
            return;

        scheduleVm.Owner.RebuildScheduleRows();
        scheduleVm.Owner.RefreshSummaries();
    }

    private async Task AddScheduleAsync(LockEditorVm? lockVm)
    {
        if (lockVm == null)
            return;

        lockVm.Model.Schedules ??= new List<LockScheduleModel>();

        var newSchedule = new LockScheduleModel
        {
            FrequencyType = FrequencyType.Once,
            FrequencyValue = 0,
            FromDateTime = WallClockScheduleTime.NormalizeLocal(_clock.LocalNow),
            ToDateTime = null
        };

        lockVm.Model.Schedules.Add(newSchedule);

        var saved = await OpenScheduleEditorAsync(newSchedule);
        if (!saved)
        {
            lockVm.Model.Schedules.Remove(newSchedule);
            lockVm.RebuildScheduleRows();
            return;
        }

        lockVm.RebuildScheduleRows();
        lockVm.RefreshSummaries();
    }

    private void RemoveSchedule(ScheduleRowVm? scheduleVm)
    {
        if (scheduleVm == null)
            return;

        scheduleVm.Owner.RemoveSchedule(scheduleVm);
    }

    private async Task EditTimeWindowAsync(LockEditorVm? lockVm)
    {
        if (lockVm == null)
            return;

        var tcs = new TaskCompletionSource<(TimeOnly Start, TimeOnly End)>();
        await _navigation.PushAsync(
            new TimeWindowEditPage(lockVm.Model.TimeWindowStart, lockVm.Model.TimeWindowEnd, tcs, _navigation, _dialogs));

        try
        {
            var (newStart, newEnd) = await tcs.Task;
            lockVm.Model.TimeWindowStart = newStart;
            lockVm.Model.TimeWindowEnd = newEnd;
            lockVm.RefreshSummaries();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task AddDependencyAsync(LockEditorVm? lockVm)
    {
        if (lockVm == null)
            return;

        await AddOrEditDependencyAsync(lockVm.Model, depToEdit: null);
        lockVm.RebuildDependencyRows();
    }

    private async Task EditDependencyAsync(DependencyRowVm? depVm)
    {
        if (depVm == null)
            return;

        await AddOrEditDependencyAsync(depVm.Owner.Model, depVm.Model);
        depVm.Owner.RebuildDependencyRows();
    }

    private void RemoveDependency(DependencyRowVm? depVm)
    {
        depVm?.Owner.RemoveDependency(depVm);
    }

    private async Task<bool> OpenScheduleEditorAsync(LockScheduleModel schedule)
    {
        var tcs = new TaskCompletionSource<bool>();

        var page = new ScheduleEditPage(
            schedule,
            saved =>
            {
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            _navigation,
            _clock);

        page.Disappearing += (_, __) =>
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetCanceled();
        };

        await _navigation.PushModalAsync(page);

        try
        {
            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private async Task AddOrEditDependencyAsync(LockModel lockModel, LockTaskDependencyModel? depToEdit)
    {
        var tcs = new TaskCompletionSource<LockTaskDependencyModel>();

        await _navigation.PushAsync(
            new TaskDependencyEditPage(_taskOptions, depToEdit, tcs, _navigation, _dialogs));

        try
        {
            var updated = await tcs.Task;
            lockModel.Dependencies ??= new List<LockTaskDependencyModel>();

            if (depToEdit == null)
            {
                lockModel.Dependencies.Add(updated);
            }
            else
            {
                depToEdit.TaskDependencyCardId = updated.TaskDependencyCardId;
                depToEdit.MetricType = updated.MetricType;
                depToEdit.TimeScope = updated.TimeScope;
                depToEdit.TargetValue = updated.TargetValue;
                depToEdit.TargetValence = updated.TargetValence;
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task SaveAsync()
    {
        var locksToSave = ToModels();
        await _locks.SaveLocksForCardAsync(_cardId, locksToSave);

        _targetLocks.Clear();
        _targetLocks.AddRange(locksToSave);

        _onChanged.Invoke();
        await _navigation.PopAsync();
    }

    private List<LockModel> ToModels()
    {
        return Locks.Select(x => x.Model).ToList();
    }

    private static List<LockModel> CloneLocks(List<LockModel> source)
    {
        return source.Select(CloneLock).ToList();
    }

    private static LockModel CloneLock(LockModel lockModel)
    {
        return new LockModel
        {
            LockId = lockModel.LockId,
            LockNumber = lockModel.LockNumber,
            CardId = lockModel.CardId,
            TimeWindowStart = lockModel.TimeWindowStart,
            TimeWindowEnd = lockModel.TimeWindowEnd,
            Schedules = lockModel.Schedules?.Select(schedule => new LockScheduleModel
            {
                ScheduleId = schedule.ScheduleId,
                LockId = schedule.LockId,
                FrequencyType = schedule.FrequencyType,
                FrequencyValue = schedule.FrequencyValue,
                FromDateTime = schedule.FromDateTime,
                ToDateTime = schedule.ToDateTime,
                IsEnabled = schedule.IsEnabled,
                Note = schedule.Note
            }).ToList() ?? new(),
            Dependencies = lockModel.Dependencies?.Select(dependency => new LockTaskDependencyModel
            {
                LockTaskDependencyId = dependency.LockTaskDependencyId,
                LockId = dependency.LockId,
                TaskDependencyCardId = dependency.TaskDependencyCardId,
                MetricType = dependency.MetricType,
                TimeScope = dependency.TimeScope,
                TargetValue = dependency.TargetValue,
                TargetValence = dependency.TargetValence
            }).ToList() ?? new()
        };
    }
}

internal sealed class LockEditorVm : ObservableObject
{
    public LockModel Model { get; }
    public List<DependencyTaskOption> TaskOptions { get; }
    public ObservableCollection<DependencyRowVm> DependencyRows { get; } = new();
    public ObservableCollection<ScheduleRowVm> ScheduleRows { get; } = new();

    public int LockNumber => Model.LockNumber;
    public string LockTitle => " ";
    public string ScheduleSummary => BuildScheduleSummary();
    public string TimeWindowSummary => BuildTimeWindowSummary();
    public bool HasNoScheduleRows => ScheduleRows.Count == 0;
    public bool HasNoDependencyRows => DependencyRows.Count == 0;

    public LockEditorVm(LockModel model, List<DependencyTaskOption> taskOptions)
    {
        Model = model;
        TaskOptions = taskOptions;

        Model.Schedules ??= new List<LockScheduleModel>();
        Model.Dependencies ??= new List<LockTaskDependencyModel>();

        RebuildScheduleRows();
        RebuildDependencyRows();
    }

    public void RebuildScheduleRows()
    {
        ScheduleRows.Clear();

        if (Model.Schedules != null)
        {
            foreach (var schedule in Model.Schedules)
                ScheduleRows.Add(new ScheduleRowVm(this, schedule));
        }

        RaisePropertyChanged(nameof(HasNoScheduleRows));
        RefreshSummaries();
    }

    public void RemoveSchedule(ScheduleRowVm scheduleVm)
    {
        Model.Schedules?.Remove(scheduleVm.Model);
        RebuildScheduleRows();
    }

    public void RefreshSummaries()
    {
        RaisePropertyChanged(nameof(LockNumber));
        RaisePropertyChanged(nameof(ScheduleSummary));
        RaisePropertyChanged(nameof(TimeWindowSummary));
    }

    public void RemoveDependency(DependencyRowVm depVm)
    {
        Model.Dependencies?.Remove(depVm.Model);
        RebuildDependencyRows();
    }

    public void RebuildDependencyRows()
    {
        DependencyRows.Clear();

        if (Model.Dependencies == null)
        {
            RaisePropertyChanged(nameof(HasNoDependencyRows));
            return;
        }

        foreach (var dependency in Model.Dependencies)
            DependencyRows.Add(new DependencyRowVm(this, dependency, TaskOptions));

        RaisePropertyChanged(nameof(HasNoDependencyRows));
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
        var start = Model.TimeWindowStart.ToString("h:mm", CultureInfo.InvariantCulture)
                    + Model.TimeWindowStart.ToString("tt", CultureInfo.InvariantCulture).ToLowerInvariant();

        var end = Model.TimeWindowEnd.ToString("h:mm", CultureInfo.InvariantCulture)
                  + Model.TimeWindowEnd.ToString("tt", CultureInfo.InvariantCulture).ToLowerInvariant();

        return $"{start} - {end}";
    }
}

internal sealed class DependencyRowVm
{
    public LockEditorVm Owner { get; }
    public LockTaskDependencyModel Model { get; }
    public List<DependencyTaskOption> TaskOptions { get; }
    public string Summary => BuildSummary();

    public DependencyRowVm(
        LockEditorVm owner,
        LockTaskDependencyModel model,
        List<DependencyTaskOption> taskOptions)
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
            ? ">="
            : "<=";

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

    public static string BuildSummary(LockScheduleModel schedule)
    {
        var time = schedule.FromDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        var start = schedule.FromDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = schedule.ToDateTime.HasValue
            ? schedule.ToDateTime.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "Never";

        var frequency = schedule.FrequencyType switch
        {
            FrequencyType.Once => $"Once at {time}",
            FrequencyType.EveryDays => $"Every {Math.Max(1, schedule.FrequencyValue)} day(s) at {time}",
            FrequencyType.EveryWeekday => $"Every weekday at {time}",
            FrequencyType.EveryMonday => $"Every Monday at {time}",
            FrequencyType.EveryTuesday => $"Every Tuesday at {time}",
            FrequencyType.EveryWednesday => $"Every Wednesday at {time}",
            FrequencyType.EveryThursday => $"Every Thursday at {time}",
            FrequencyType.EveryFriday => $"Every Friday at {time}",
            FrequencyType.EverySaturday => $"Every Saturday at {time}",
            FrequencyType.EverySunday => $"Every Sunday at {time}",
            FrequencyType.EveryWeeks => $"Every {Math.Max(1, schedule.FrequencyValue)} week(s) at {time}",
            FrequencyType.EveryMonths => $"Every {Math.Max(1, schedule.FrequencyValue)} month(s) at {time}",
            FrequencyType.EveryYears => $"Every {Math.Max(1, schedule.FrequencyValue)} year(s) at {time}",
            _ => schedule.FrequencyType.ToString()
        };

        return $"{frequency} - From: {start} - Ends: {end}";
    }
}
