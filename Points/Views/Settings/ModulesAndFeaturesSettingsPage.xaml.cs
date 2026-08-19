using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class ModulesAndFeaturesSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;

    public ModulesAndFeaturesSettingsPage(
        ISettingsService settings,
        IAppNavigationService navigation,
        Func<Task>? refreshHomeAsync = null)
    {
        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        BindingContext = new ModulesAndFeaturesSettingsViewModel(
            settings,
            async () =>
            {
                if (refreshHomeAsync != null)
                    await refreshHomeAsync();

                await ReturnToSettingsPageAsync();
            });
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await _navigation.PopAsync();
    }
}
