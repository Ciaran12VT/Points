using Points.Services.Navigation;
using Points.Services.Notifications;
using Points.Services.Persistence;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class ModulesAndFeaturesSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly ModulesAndFeaturesSettingsViewModel _viewModel;
    private bool _hasAppeared;

    public ModulesAndFeaturesSettingsPage(
        ISettingsService settings,
        IActiveCardNotificationAvailabilityService notificationAvailability,
        IAppNavigationService navigation,
        Func<Task>? reconcileNotificationAsync = null,
        Func<Task>? refreshHomeAsync = null)
    {
        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _viewModel = new ModulesAndFeaturesSettingsViewModel(
            settings,
            notificationAvailability,
            reconcileNotificationAsync,
            async () =>
            {
                if (refreshHomeAsync != null)
                    await refreshHomeAsync();

                await ReturnToSettingsPageAsync();
            });
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var refreshAvailability = _hasAppeared;
        _hasAppeared = true;

        try
        {
            await _viewModel.Initialization;

            if (refreshAvailability)
                await _viewModel.RefreshNotificationAvailabilityAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Could not initialize Modules & Features settings: {ex}");
        }
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await _navigation.PopAsync();
    }
}
