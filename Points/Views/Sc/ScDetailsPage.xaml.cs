using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Sc;
using System.Collections.Specialized;

namespace Points.Views.Sc;

public partial class ScDetailsPage : ContentPage
{
    private INotifyCollectionChanged? _stepsNotify;

    public ScDetailsPage(
        ScCardModel model,
        Action<ScCardModel> onSaved,
        Func<ScCardModel, Task> onDelete,
        List<string> availableTagsList,
        IAchievementService achievements,
        IActivityService activity,
        IUdmdService udmd,
        IClock clock,
        ITimeZoneService timeZoneService,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        BindingContext = new ScDetailsViewModel(
            model,
            onSaved,
            onDelete,
            availableTagsList,
            achievements,
            activity,
            udmd,
            clock,
            timeZoneService,
            navigation,
            dialogs);

        Loaded += async (_, __) =>
        {
            await Task.Delay(50);
            await ScrollToBottomAsync(animated: false);
        };
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged -= Steps_CollectionChanged;

        _stepsNotify = null;

        var vm = BindingContext;
        var stepsProp = vm?.GetType().GetProperty("Steps")?.GetValue(vm);

        _stepsNotify = stepsProp as INotifyCollectionChanged;
        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged += Steps_CollectionChanged;
    }

    private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(50);
            await ScrollToBottomAsync(animated: true);
        });
    }

    private Task ScrollToBottomAsync(bool animated)
    {
        return MainScroll.ScrollToAsync(BottomAnchor, ScrollToPosition.End, animated);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is ScDetailsViewModel vm)
            vm.StopTimer();
    }
}
