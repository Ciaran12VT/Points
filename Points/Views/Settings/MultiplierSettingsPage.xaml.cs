using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Settings;

public partial class MultipliersSettingsPage : ContentPage
{
    public MultipliersSettingsPage(ISettingsService settings)
    {
        InitializeComponent();
        BindingContext = new MultipliersSettingsViewModel(settings, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}
