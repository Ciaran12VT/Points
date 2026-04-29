using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Settings;

public partial class ModulesAndFeaturesSettingsPage : ContentPage
{
    public ModulesAndFeaturesSettingsPage(ISettingsService settings)
    {
        InitializeComponent();
        BindingContext = new ModulesAndFeaturesSettingsViewModel(settings, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}
