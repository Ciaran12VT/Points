using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Shared;

public partial class EditLocksPage : ContentPage
{
    private readonly long _cardId;
    private readonly IDbService _db;
    private readonly List<LockModel> _targetLocks;     // the list on the TatCardModel
    private readonly Action _onChanged;
    private readonly List<DependencyTaskOption> _dependencyOptions;

    private readonly EditLocksVm _vm;

    public EditLocksPage(long cardId, List<LockModel> locks, IDbService db, List<DependencyTaskOption> dependencyOptions, Action onChanged)
	{
		InitializeComponent();
        _cardId = cardId;
        _db = db;
        _targetLocks = locks;
        _onChanged = onChanged;
        _dependencyOptions = dependencyOptions;

        // work on a copy; only overwrite original on Save
        _vm = new EditLocksVm(cardId, CloneLocks(locks), _dependencyOptions);
        BindingContext = _vm;
    }


    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // When you later implement actual editing, call _onChanged() only after save.
        // For plumbing now, you can leave this out or call it unconditionally.
        _onChanged?.Invoke();
    }

    private void OnAddLockClicked(object sender, EventArgs e)
    {
        _vm.AddLock();
    }

    private async void OnRemoveLockClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is LockEditorVm lockVm)
        {
            await _db.DeleteLockModelAsync(lockVm.Model);
            _vm.RemoveLock(lockVm);
        }            
    }

    private async void OnEditScheduleClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ScheduleRowVm scheduleVm)
            return;

        var saved = await EditScheduleAsync(scheduleVm.Owner, scheduleVm.Model);
        if (!saved)
            return;

        scheduleVm.Owner.RebuildScheduleRows();
        scheduleVm.Owner.RefreshSummaries();
    }

    private async void OnAddScheduleClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not LockEditorVm lockVm)
            return;

        lockVm.Model.Schedules ??= new List<LockScheduleModel>();

        var newSchedule = new LockScheduleModel
        {
            FrequencyType = FrequencyType.Once,
            FrequencyValue = 0,
            FromDateTime = DateTime.Now,
            ToDateTime = null,
            // If your LockScheduleModel has these:
            // IsEnabled = true,
            // Note = ""
        };

        // Add first so edits apply to the same instance
        lockVm.Model.Schedules.Add(newSchedule);

        var saved = await EditScheduleAsync(lockVm, newSchedule);
        if (!saved)
        {
            // If they cancelled, remove the placeholder schedule we inserted
            lockVm.Model.Schedules.Remove(newSchedule);
            lockVm.RebuildScheduleRows();
            return;
        }

        lockVm.RebuildScheduleRows();
        lockVm.RefreshSummaries();
    }

    private void OnRemoveScheduleClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ScheduleRowVm scheduleVm)
            return;

        scheduleVm.Owner.Model.Schedules.Remove(scheduleVm.Model);
        scheduleVm.Owner.RebuildScheduleRows();
    }

    private async void OnEditTimeWindowClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not LockEditorVm lockVm)
            return;

        await EditTimeWindowAsync(lockVm.Model);

        lockVm.RefreshSummaries();
    }

    private async void OnAddDependencyClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not LockEditorVm lockVm)
            return;

        // create new dep for this lock
        await AddOrEditDependencyAsync(lockVm.Model, depToEdit: null);

        // refresh the lock row UI if needed
        lockVm.RebuildDependencyRows(); // only if you cache summaries
    }

    private async void OnEditDependencyClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not DependencyRowVm depVm)
            return;

        // We need the underlying domain model to edit in-place.
        // DependencyRowVm must expose it (recommended).
        var depModel = depVm.Model; // add this property if not present

        await AddOrEditDependencyAsync(depVm.Owner.Model, depModel);

        depVm.Owner.RebuildDependencyRows(); // only if cached
    }

    private void OnRemoveDependencyClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not DependencyRowVm depVm)
            return;

        depVm.Owner.RemoveDependency(depVm);
    }


    private async Task<bool> EditScheduleAsync(LockEditorVm owner, LockScheduleModel schedule)
    {
        var tcs = new TaskCompletionSource<bool>();

        var page = new Points.Views.Schedules.ScheduleEditPage(
            schedule,
            async _ =>
            {
                // ScheduleEditPage already mutates the schedule instance in-place
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            });

        // If the user cancels/back-navigates, ScheduleEditPage does PopModalAsync without calling onSaved.
        // We treat "modal disappeared without Save" as cancel.
        page.Disappearing += (_, __) =>
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetCanceled();
        };

        await Shell.Current.Navigation.PushModalAsync(page);

        try
        {
            return await tcs.Task; // true = saved
        }
        catch (TaskCanceledException)
        {
            return false; // canceled
        }
    }

    private async Task EditTimeWindowAsync(LockModel lockModel)
    {
        var tcs = new TaskCompletionSource<(TimeOnly Start, TimeOnly End)>();

        await Shell.Current.Navigation.PushAsync(
            new TimeWindowEditPage(lockModel.TimeWindowStart, lockModel.TimeWindowEnd, tcs));

        try
        {
            var (newStart, newEnd) = await tcs.Task;
            lockModel.TimeWindowStart = newStart;
            lockModel.TimeWindowEnd = newEnd;

            // If you maintain a summary string, refresh it here.
            // lockVm.RefreshSummaries();
        }
        catch (TaskCanceledException) { }
    }

    private async Task AddOrEditDependencyAsync(LockModel lockModel, LockTaskDependencyModel? depToEdit)
    {
        var tcs = new TaskCompletionSource<LockTaskDependencyModel>();

        await Shell.Current.Navigation.PushAsync(
            new TaskDependencyEditPage(_dependencyOptions, depToEdit, tcs));

        try
        {
            var updated = await tcs.Task;

            lockModel.Dependencies ??= new List<LockTaskDependencyModel>();

            if (depToEdit == null)
            {
                // ADD
                lockModel.Dependencies.Add(updated);
            }
            else
            {
                // EDIT (overwrite in-place)
                depToEdit.TaskDependencyCardId = updated.TaskDependencyCardId;
                depToEdit.MetricType = updated.MetricType;
                depToEdit.TimeScope = updated.TimeScope;
                depToEdit.TargetValue = updated.TargetValue;
                depToEdit.TargetValence = updated.TargetValence;   // 🔴 REQUIRED
            }
        }
        catch (TaskCanceledException) { }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // 1) materialize locks from editor VMs
        var locksToSave = _vm.ToModels();

        // 2) persist
        await _db.SaveLocksForCardAsync(_cardId, locksToSave);

        // 3) update the card model list in-memory
        _targetLocks.Clear();
        _targetLocks.AddRange(locksToSave);

        // 4) notify caller (TatDetailsPage updates its summary label)
        _onChanged?.Invoke();

        await Navigation.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    // --- cloning helpers (keep it simple + explicit) ---
    private static List<LockModel> CloneLocks(List<LockModel> source)
        => source.Select(CloneLock).ToList();

    private static LockModel CloneLock(LockModel l) => new LockModel
    {
        LockId = l.LockId,
        LockNumber = l.LockNumber,
        CardId = l.CardId,
        TimeWindowStart = l.TimeWindowStart,
        TimeWindowEnd = l.TimeWindowEnd,
        Schedules = l.Schedules?.Select(s => new LockScheduleModel
        {
            ScheduleId = s.ScheduleId,
            LockId = s.LockId,
            FrequencyType = s.FrequencyType,
            FrequencyValue = s.FrequencyValue,
            FromDateTime = s.FromDateTime,
            ToDateTime = s.ToDateTime
        }).ToList() ?? new(),
        Dependencies = l.Dependencies?.Select(d => new LockTaskDependencyModel
        {
            LockTaskDependencyId = d.LockTaskDependencyId,
            LockId = d.LockId,
            TaskDependencyCardId = d.TaskDependencyCardId,
            MetricType = d.MetricType,
            TimeScope = d.TimeScope,
            TargetValue = d.TargetValue,
            TargetValence = d.TargetValence
        }).ToList() ?? new()
    };
}