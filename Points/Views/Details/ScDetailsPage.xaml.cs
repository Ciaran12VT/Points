using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;
using Points.Views.Schedules;
using System.Collections.Specialized;

namespace Points.Views.Details;

public partial class ScDetailsPage : ContentPage
{
    INotifyCollectionChanged? _stepsNotify;
    private readonly TatCardModel _model;
    private readonly IDbService _db;
    private readonly List<string> _allTags;

    public ScDetailsPage(ScCardModel model, Action<ScCardModel> onSaved, Action<ScCardModel> onDelete, List<string> availableTagsList, IDbService db)
    {
        InitializeComponent();
        BindingContext = new ScDetailsViewModel(model, onSaved, onDelete, availableTagsList, db);
        _allTags = availableTagsList;
        _model = model;
        _db = db;
        // 1) First scroll: only after the page is actually loaded + laid out
        Loaded += async (_, __) =>
        {
            // Let layout + CollectionView item realization happen
            await Task.Delay(50);

            await ScrollToBottomAsync(animated: false);
        };

        ScheduleSummaryLabel.Text =
            _model.Schedules.Count == 0 ? "None" :
            _model.Schedules.Count == 1 ? "1 schedule" :
            $"{_model.Schedules.Count} schedules";
    }
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Unhook previous
        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged -= Steps_CollectionChanged;

        // Hook current Steps collection (if it supports notifications)
        _stepsNotify = null;

        // If your VM is strongly-typed, cast it instead of dynamic.
        var vm = BindingContext;
        var stepsProp = vm?.GetType().GetProperty("Steps")?.GetValue(vm);

        _stepsNotify = stepsProp as INotifyCollectionChanged;
        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged += Steps_CollectionChanged;
    }

    private async void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 2) Re-scroll whenever steps are added/removed (esp. after AddStepCommand)
        // Dispatch ensures we run after MAUI updates the visual tree.
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(50);
            await ScrollToBottomAsync(animated: true);
        });
    }

    private Task ScrollToBottomAsync(bool animated)
    {
        // ScrollView -> element overload is the most reliable when you have an anchor.
        return MainScroll.ScrollToAsync(BottomAnchor, ScrollToPosition.End, animated);
    }

    private async void OnEditTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ScDetailsViewModel vm)
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
        if (BindingContext is ScDetailsViewModel vm)
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

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is ScDetailsViewModel vm)
            vm.StopTimer();
    }
}
