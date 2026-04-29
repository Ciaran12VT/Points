using Points.Services.Sqlite.Interfaces;

namespace Points.Views.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly IDatabaseMaintenanceService _databaseMaintenance;
    private readonly IDatabaseInitializationService _databaseLifecycle;
    private readonly INotificationLogService _notificationLogs;
    private readonly ISettingsService _settings;

    public SettingsPage(
        IDatabaseMaintenanceService databaseMaintenance,
        IDatabaseInitializationService databaseLifecycle,
        INotificationLogService notificationLogs,
        ISettingsService settings)
    {
        InitializeComponent();
        _databaseMaintenance = databaseMaintenance;
        _databaseLifecycle = databaseLifecycle;
        _notificationLogs = notificationLogs;
        _settings = settings;
    }

    private async void OnDatabaseSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new DatabaseSettingsPage(_databaseMaintenance, _databaseLifecycle));
    }

    private async void OnMultipliersSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new MultipliersSettingsPage(_settings));
    }

    private async void OnModulesAndFeaturesSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new ModulesAndFeaturesSettingsPage(_settings));
    }

    private async void OnNotificationsLogClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PushAsync(new NotificationLogPage(_notificationLogs));
    }
}
