using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Settings;

public partial class ModulesAndFeaturesSettingsPage : ContentPage
{
    public ModulesAndFeaturesSettingsPage(IDbService db)
    {
        InitializeComponent();
        BindingContext = new ModulesAndFeaturesSettingsViewModel(db, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}