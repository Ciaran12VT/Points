using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Settings;

public partial class MultipliersSettingsPage : ContentPage
{
    public MultipliersSettingsPage(IDbService db)
    {
        InitializeComponent();
        BindingContext = new MultipliersSettingsViewModel(db, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}