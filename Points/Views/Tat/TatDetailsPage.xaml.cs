using Points.Global;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Tat;
using Points.Views.Shared;

namespace Points.Views.Tat;

public partial class TatDetailsPage : ContentPage
{
    public TatDetailsPage(
        TatCardModel model,
        Action<TatCardModel> onSaved,
        Action<TatCardModel> onDelete,
        List<string> availableTagsList,
        ILockService locks,
        IActivityService activity,
        IUdmdService udmd,
        List<DependencyTaskOption> dependencyOptions,
        IClock clock,
        ITimeZoneService timeZoneService,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        var vm = new TatDetailsViewModel(
            model,
            onSaved,
            onDelete,
            availableTagsList,
            locks,
            activity,
            udmd,
            dependencyOptions,
            clock,
            timeZoneService,
            navigation,
            dialogs);

        BindingContext = vm;

        vm.IsLocksEnabled = SettingsProvider.IsLocksEnabled;
        vm.IsValueRatesEnabled = SettingsProvider.IsValueRatesEnabled;
        vm.IsSchedulesEnabled = SettingsProvider.IsSchedulesEnabled;
        Loaded += OnPageLoaded;
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

        if (!string.IsNullOrWhiteSpace(TitleEntry.Text))
            return;

        if (!TitleEntry.IsEnabled || TitleEntry.IsReadOnly || !TitleEntry.IsVisible)
            return;

        await Task.Delay(50);

        for (int i = 0; i < 3; i++)
        {
            MainThread.BeginInvokeOnMainThread(() => TitleEntry.Focus());
            await Task.Delay(50);

            if (TitleEntry.IsFocused)
                return;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is TatDetailsViewModel vm)
            vm.StopTimer();
    }
}
