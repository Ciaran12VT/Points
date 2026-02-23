using Points.Models;
using Points.Services;
using Points.ViewModels;
using Points.Views.Schedules;
using Points.Views.Shared;
using System.Diagnostics;

namespace Points.Views.Details;

public partial class TatDetailsPage : ContentPage
{
    private readonly TatCardModel _model;
    private readonly IDbService _db;
    private readonly List<string> _allTags;
    private readonly List<DependencyTaskOption> _dependencyOptions;

    public TatDetailsPage(TatCardModel model, Action<TatCardModel> onSaved, Action<TatCardModel> onDelete, List<string> availableTagsList, Services.IDbService db, List<DependencyTaskOption> dependencyOptions)
    {
        InitializeComponent();
        BindingContext = new TatDetailsViewModel(model, onSaved, onDelete, availableTagsList);
        _model = model;
        _db = db;
        _allTags = availableTagsList;
        _dependencyOptions = dependencyOptions;
        Loaded += OnPageLoaded;

        ScheduleSummaryLabel.Text =
            _model.Schedules.Count == 0 ? "None" :
            _model.Schedules.Count == 1 ? "1 schedule" :
            $"{_model.Schedules.Count} schedules";

        LocksSummaryLabel.Text =
            _model.Locks.Count == 0 ? "None" :
            _model.Locks.Count == 1 ? "1 lock" :
            $"{_model.Locks.Count} locks";
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await TryFocusTitleIfEmptyAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TryFocusTitleIfEmptyAsync();
    }

    private async Task TryFocusTitleIfEmptyAsync()
    {
        if (TitleEntry == null)
            return;

        // If text is not empty, do nothing.
        if (!string.IsNullOrWhiteSpace(TitleEntry.Text))
            return;

        // If it can't be focused, do nothing.
        if (!TitleEntry.IsEnabled || TitleEntry.IsReadOnly || !TitleEntry.IsVisible)
            return;

        // Let navigation + layout settle
        await Task.Delay(50);

        // Focus can still fail; retry a couple of times
        for (int i = 0; i < 3; i++)
        {
            MainThread.BeginInvokeOnMainThread(() => TitleEntry.Focus());
            await Task.Delay(50);

            if (TitleEntry.IsFocused)
                return;
        }
    }

    private async void OnEditTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not TatDetailsViewModel vm)
            return;

        var initial = (vm.Tags ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var page = new MultiSelectPickerPage(
            "Select Tags",
            _allTags,
            initial,
            false
        );

        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result;
        if (result == null)
            return; // cancelled

        vm.Tags = string.Join(", ", result);
    }

    private void OnClearTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is TatDetailsViewModel vm)
            vm.Tags = "";
    }

    private async void OnEditActiveTimeClicked(object sender, EventArgs e)
    {
        var tcs = new TaskCompletionSource<List<ActivityModel>>();

        var page = new Points.Views.Details.EditActiveTimePage(_model.Activity, tcs, _db);
        await Navigation.PushAsync(page);

        try
        {
            var edited = await tcs.Task;   // user hit Save
            _model.Activity = edited;      // store it wherever you keep it
        }
        catch (TaskCanceledException)
        {
            // user backed out, ignore
        }
    }

    private async void OnEditSchedulesClicked(object sender, EventArgs e)
    {
        // Require a persisted card so schedules can be keyed by CardId
        if (_model.Id <= 0)
        {
            ShowError("Please tap OK to save the tracker first, then add schedules.");
            return;
        }

        // For now, delegates are null (in-memory-only UI).
        // We'll wire these to DB repository methods next.
        await Shell.Current.Navigation.PushAsync(
            new CardSchedulesPage(
                cardId: _model.Id,
                schedules: _model.Schedules,
                onChanged: () =>
                {
                    // simplest summary update (you can improve formatting later)
                    ScheduleSummaryLabel.Text = _model.Schedules.Count == 0 ? "None"
                        : _model.Schedules.Count == 1 ? "1 schedule"
                        : $"{_model.Schedules.Count} schedules";
                }));

    }

    private async void OnEditLocksClicked(object sender, EventArgs e)
    {
        // Decide whether to require persistence:
        // - If locks are keyed by CardId and saved via SaveLocksForCardAsync(cardId, ...),
        //   you need a persisted card.
        // - If your UI is in-memory-only until user taps OK/save, you can skip this guard.
        if (_model.Id <= 0)
        {
            ShowError("Please tap OK to save the tracker first, then add locks.");
            return;
        }

        // For now this is a stub page; later it becomes the real editor.
        await Shell.Current.Navigation.PushAsync(
            new EditLocksPage(
                cardId: _model.Id,
                locks: _model.Locks,
                db: _db,
                dependencyOptions: _dependencyOptions,
                onChanged: () =>
                {
                    LocksSummaryLabel.Text = _model.Locks.Count == 0 ? "None"
                        : _model.Locks.Count == 1 ? "1 lock"
                        : $"{_model.Locks.Count} locks";
                }));
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
    }

    private async void OnSetActiveTimeTargetClicked(object sender, EventArgs e)
    {
        if (BindingContext is TatDetailsViewModel typedVm)
        {
            // 2) Push your picker page
            // This assumes your DurationPickerPage returns a TimeSpan (or null if cancelled).
            var page = new DurationPickerPage(typedVm.TargetActiveTime);

            // OPTION A: if DurationPickerPage exposes a TaskCompletionSource result
            await Shell.Current.Navigation.PushAsync(page);

            var result = await page.Result; // e.g. Task<TimeSpan?>
                                            // User hit Cancel → leave everything as-is
            if (page.WasCancelled)
                return;

            // User hit Reset → set underlying property to null
            if (result is null)
            {
                typedVm.TargetActiveTime = null;
                typedVm.RaisePropertyChanged(nameof(typedVm.HasTargetActiveTime));
                typedVm.RaisePropertyChanged(nameof(typedVm.ActiveTimeTargetLabelColor));
                return;
            }

            var totalHours = (int)result.Value.TotalHours;
            var formatted = $"{totalHours}:{result.Value.Minutes:D2}:{result.Value.Seconds:D2}";

            //Hold the target in a property in the VM
            typedVm.TargetActiveTime = result;
            typedVm.RaisePropertyChanged(nameof(typedVm.HasTargetActiveTime));
            typedVm.RaisePropertyChanged(nameof(typedVm.ActiveTimeTargetLabelColor));
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is TatDetailsViewModel vm)
            vm.StopTimer();
    }

}