using Points.Services.Backup;
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
    private readonly IHardModePenaltyService _hardModePenalties;
    private readonly IUserMultiplierService _userMultipliers;
    private readonly IClock _clock;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IBackupFileStorageService _backupFileStorage;
    private readonly IScheduledBackupConfigStore _scheduledBackupConfigStore;
    private readonly IScheduledBackupLogStore _scheduledBackupLogStore;
    private readonly IGoogleDriveBackupConnector _googleDriveBackupConnector;
    private readonly IScheduledBackupWorkScheduler _scheduledBackupWorkScheduler;

    public Command OpenDatabaseSettingsCommand { get; }
    public Command OpenMultipliersSettingsCommand { get; }
    public Command OpenModulesAndFeaturesSettingsCommand { get; }
    public Command OpenDefaultsAndMiscSettingsCommand { get; }
    public Command OpenNotificationsLogCommand { get; }

    public SettingsPage(
        IDatabaseMaintenanceService databaseMaintenance,
        IDatabaseInitializationService databaseLifecycle,
        INotificationLogService notificationLogs,
        ISettingsService settings,
        IHardModePenaltyService hardModePenalties,
        IUserMultiplierService userMultipliers,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock,
        ITimeZoneService timeZoneService,
        IBackupFileStorageService backupFileStorage,
        IScheduledBackupConfigStore scheduledBackupConfigStore,
        IScheduledBackupLogStore scheduledBackupLogStore,
        IGoogleDriveBackupConnector googleDriveBackupConnector,
        IScheduledBackupWorkScheduler scheduledBackupWorkScheduler)
    {
        OpenDatabaseSettingsCommand = new Command(async () => await OpenDatabaseSettingsAsync());
        OpenMultipliersSettingsCommand = new Command(async () => await OpenMultipliersSettingsAsync());
        OpenModulesAndFeaturesSettingsCommand = new Command(async () => await OpenModulesAndFeaturesSettingsAsync());
        OpenDefaultsAndMiscSettingsCommand = new Command(async () => await OpenDefaultsAndMiscSettingsAsync());
        OpenNotificationsLogCommand = new Command(async () => await OpenNotificationsLogAsync());

        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _databaseMaintenance = databaseMaintenance;
        _databaseLifecycle = databaseLifecycle;
        _notificationLogs = notificationLogs;
        _settings = settings;
        _hardModePenalties = hardModePenalties ?? throw new ArgumentNullException(nameof(hardModePenalties));
        _userMultipliers = userMultipliers ?? throw new ArgumentNullException(nameof(userMultipliers));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _backupFileStorage = backupFileStorage ?? throw new ArgumentNullException(nameof(backupFileStorage));
        _scheduledBackupConfigStore = scheduledBackupConfigStore ?? throw new ArgumentNullException(nameof(scheduledBackupConfigStore));
        _scheduledBackupLogStore = scheduledBackupLogStore ?? throw new ArgumentNullException(nameof(scheduledBackupLogStore));
        _googleDriveBackupConnector = googleDriveBackupConnector ?? throw new ArgumentNullException(nameof(googleDriveBackupConnector));
        _scheduledBackupWorkScheduler = scheduledBackupWorkScheduler ?? throw new ArgumentNullException(nameof(scheduledBackupWorkScheduler));
    }

    private async Task OpenDatabaseSettingsAsync()
    {
        await _navigation.PushAsync(new DatabaseSettingsPage(
            _databaseMaintenance,
            _databaseLifecycle,
            _settings,
            _backupFileStorage,
            _scheduledBackupConfigStore,
            _scheduledBackupLogStore,
            _googleDriveBackupConnector,
            _scheduledBackupWorkScheduler,
            _navigation,
            _dialogs,
            _clock,
            _timeZoneService));
    }

    private async Task OpenMultipliersSettingsAsync()
    {
        await _navigation.PushAsync(new MultipliersSettingsPage(_settings, _hardModePenalties, _userMultipliers, _clock, _navigation));
    }

    private async Task OpenModulesAndFeaturesSettingsAsync()
    {
        await _navigation.PushAsync(new ModulesAndFeaturesSettingsPage(_settings, _navigation));
    }

    private async Task OpenDefaultsAndMiscSettingsAsync()
    {
        await _navigation.PushAsync(new DefaultsAndMiscSettingsPage(_settings, _navigation));
    }

    private async Task OpenNotificationsLogAsync()
    {
        await _navigation.PushAsync(new NotificationLogPage(_notificationLogs, _clock, _timeZoneService));
    }
}
