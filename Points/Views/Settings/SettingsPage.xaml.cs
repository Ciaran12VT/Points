using Points.ViewModels;

namespace Points.Views.Settings;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(Services.IDbService _db)
    {
        InitializeComponent();
        BindingContext = new SettingsViewModel(_db);
    }
}