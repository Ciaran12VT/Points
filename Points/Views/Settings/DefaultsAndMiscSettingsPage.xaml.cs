using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class DefaultsAndMiscSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;

    public DefaultsAndMiscSettingsPage(
        ISettingsService settings,
        IAppNavigationService navigation)
    {
        InitializeComponent();

        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        BindingContext = new DefaultsAndMiscSettingsViewModel(settings, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await _navigation.PopAsync();
    }
}
