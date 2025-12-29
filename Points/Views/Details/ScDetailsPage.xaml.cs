using Points.Models;
using Points.ViewModels;
using System.Collections.Specialized;

namespace Points.Views.Details;

public partial class ScDetailsPage : ContentPage
{
    INotifyCollectionChanged? _stepsNotify;
    private readonly List<string> _allTags;

    public ScDetailsPage(ScCardModel model, Action<ScCardModel> onSaved, Action<ScCardModel> onDelete, List<string> availableTagsList)
    {
        InitializeComponent();
        BindingContext = new ScDetailsViewModel(model, onSaved, onDelete, availableTagsList);
        _allTags = availableTagsList;
        // 1) First scroll: only after the page is actually loaded + laid out
        Loaded += async (_, __) =>
        {
            // Let layout + CollectionView item realization happen
            await Task.Delay(50);

            await ScrollToBottomAsync(animated: false);
        };
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
}
