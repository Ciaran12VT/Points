using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class ModulesAndFeaturesSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;

    public ModulesAndFeaturesSettingsPage(
        ISettingsService settings,
        IAppNavigationService navigation)
    {
        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        BindingContext = new ModulesAndFeaturesSettingsViewModel(settings, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await _navigation.PopAsync();
    }
}
