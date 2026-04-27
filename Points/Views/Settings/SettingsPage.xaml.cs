using Points.Services.Sqlite.Interfaces;

namespace Points.Views.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly IDbService _db;

    public SettingsPage(IDbService db)
    {
        InitializeComponent();
        _db = db;
    }

    private async void OnDatabaseSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new DatabaseSettingsPage(_db));
    }

    private async void OnMultipliersSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new MultipliersSettingsPage(_db));
    }

    private async void OnModulesAndFeaturesSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new ModulesAndFeaturesSettingsPage(_db));
    }

    private async void OnNotificationsLogClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new NotificationLogPage(_db));
    }
}
