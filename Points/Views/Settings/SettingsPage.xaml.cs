using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Views.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly IDatabaseMaintenanceService _databaseMaintenance;
    private readonly IDatabaseInitializationService _databaseLifecycle;
    private readonly INotificationLogService _notificationLogs;
    private readonly ISettingsService _settings;
    private readonly IClock _clock;
    private readonly ITimeZoneService _timeZoneService;

    public Command OpenDatabaseSettingsCommand { get; }
    public Command OpenMultipliersSettingsCommand { get; }
    public Command OpenModulesAndFeaturesSettingsCommand { get; }
    public Command OpenNotificationsLogCommand { get; }

    public SettingsPage(
        IDatabaseMaintenanceService databaseMaintenance,
        IDatabaseInitializationService databaseLifecycle,
        INotificationLogService notificationLogs,
        ISettingsService settings,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock,
        ITimeZoneService timeZoneService)
    {
        OpenDatabaseSettingsCommand = new Command(async () => await OpenDatabaseSettingsAsync());
        OpenMultipliersSettingsCommand = new Command(async () => await OpenMultipliersSettingsAsync());
        OpenModulesAndFeaturesSettingsCommand = new Command(async () => await OpenModulesAndFeaturesSettingsAsync());
        OpenNotificationsLogCommand = new Command(async () => await OpenNotificationsLogAsync());

        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _databaseMaintenance = databaseMaintenance;
        _databaseLifecycle = databaseLifecycle;
        _notificationLogs = notificationLogs;
        _settings = settings;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
    }

    private async Task OpenDatabaseSettingsAsync()
    {
        await _navigation.PushAsync(new DatabaseSettingsPage(_databaseMaintenance, _databaseLifecycle, _navigation, _dialogs, _clock));
    }

    private async Task OpenMultipliersSettingsAsync()
    {
        await _navigation.PushAsync(new MultipliersSettingsPage(_settings, _navigation));
    }

    private async Task OpenModulesAndFeaturesSettingsAsync()
    {
        await _navigation.PushAsync(new ModulesAndFeaturesSettingsPage(_settings, _navigation));
    }

    private async Task OpenNotificationsLogAsync()
    {
        await _navigation.PushAsync(new NotificationLogPage(_notificationLogs, _clock, _timeZoneService));
    }
}
