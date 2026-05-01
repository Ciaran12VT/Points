using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class MultipliersSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;

    public MultipliersSettingsPage(
        ISettingsService settings,
        IAppNavigationService navigation)
    {
        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        BindingContext = new MultipliersSettingsViewModel(settings, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await _navigation.PopAsync();
    }
}
