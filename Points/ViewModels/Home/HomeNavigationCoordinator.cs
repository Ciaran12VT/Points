using Points.Global;
using Points.Models;
using Points.Services.Backup;
using Points.Services.Navigation;
using Points.Services.Notifications;
using Points.Services.Persistence;
using Points.Services.Premium;
using Points.Services.Time;
using Points.Services.Watch;
using Points.ViewModels.Leaderboard;
using Points.Views.Leaderboard;

namespace Points.ViewModels.Home
{
    internal sealed class HomeNavigationCoordinator
    {
        private readonly ICardReadService _cardReader;
        private readonly ICardWriteService _cardWriter;
        private readonly IDatabaseMaintenanceService _databaseMaintenance;
        private readonly IDatabaseInitializationService _databaseLifecycle;
        private readonly IReportService _reports;
        private readonly IGoalService _goals;
        private readonly IAchievementService _achievements;
        private readonly INotificationLogService _notificationLogs;
        private readonly ISettingsService _settings;
        private readonly IHardModePenaltyService _hardModePenalties;
        private readonly IUserMultiplierService _userMultipliers;
        private readonly IPlannerService _planner;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly IPopupService _popups;
        private readonly IClock _clock;
        private readonly IBackupFileStorageService _backupFileStorage;
        private readonly IScheduledBackupConfigStore _scheduledBackupConfigStore;
        private readonly IScheduledBackupLogStore _scheduledBackupLogStore;
        private readonly IGoogleDriveBackupConnector _googleDriveBackupConnector;
        private readonly IScheduledBackupWorkScheduler _scheduledBackupWorkScheduler;
        private readonly IPremiumSubscriptionService _premiumSubscriptions;
        private readonly IWatchSnapshotPublishService _watchSnapshots;
        private readonly IWatchShortcutSettingsService _watchShortcuts;
        private readonly IActiveCardNotificationAvailabilityService _activeCardNotificationAvailability;
        private readonly IReadOnlyList<HomePageModel> _pages;
        private readonly HomePageStateCoordinator _pageState;
        private readonly HomeCardWorkflowCoordinator _cardWorkflow;
        private readonly HomeDashboardShortcutWorkflowCoordinator _dashboardShortcuts;
        private readonly Func<Task?> _getInitialization;
        private readonly Func<Task> _reconcileNotificationAsync;
        private readonly Func<DateTime, DateTime, Task> _refreshHomeAsync;
        private readonly Func<DateTime> _getRangeStart;
        private readonly Func<DateTime> _getRangeEnd;
        private readonly Action<DateTime> _setRangeStart;
        private readonly Action<DateTime> _setRangeEnd;
        private readonly Action<string> _notifyPropertyChanged;

        public HomeNavigationCoordinator(
            ICardReadService cardReader,
            ICardWriteService cardWriter,
            IDatabaseMaintenanceService databaseMaintenance,
            IDatabaseInitializationService databaseLifecycle,
            IReportService reports,
            IGoalService goals,
            IAchievementService achievements,
            INotificationLogService notificationLogs,
            ISettingsService settings,
            IHardModePenaltyService hardModePenalties,
            IUserMultiplierService userMultipliers,
            IPlannerService planner,
            ITimeZoneService timeZoneService,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            IPopupService popups,
            IClock clock,
            IBackupFileStorageService backupFileStorage,
            IScheduledBackupConfigStore scheduledBackupConfigStore,
            IScheduledBackupLogStore scheduledBackupLogStore,
            IGoogleDriveBackupConnector googleDriveBackupConnector,
            IScheduledBackupWorkScheduler scheduledBackupWorkScheduler,
            IPremiumSubscriptionService premiumSubscriptions,
            IWatchSnapshotPublishService watchSnapshots,
            IWatchShortcutSettingsService watchShortcuts,
            IActiveCardNotificationAvailabilityService activeCardNotificationAvailability,
            IReadOnlyList<HomePageModel> pages,
            HomePageStateCoordinator pageState,
            HomeCardWorkflowCoordinator cardWorkflow,
            HomeDashboardShortcutWorkflowCoordinator dashboardShortcuts,
            Func<Task?> getInitialization,
            Func<Task> reconcileNotificationAsync,
            Func<DateTime, DateTime, Task> refreshHomeAsync,
            Func<DateTime> getRangeStart,
            Func<DateTime> getRangeEnd,
            Action<DateTime> setRangeStart,
            Action<DateTime> setRangeEnd,
            Action<string> notifyPropertyChanged)
        {
            _cardReader = cardReader;
            _cardWriter = cardWriter;
            _databaseMaintenance = databaseMaintenance;
            _databaseLifecycle = databaseLifecycle;
            _reports = reports;
            _goals = goals;
            _achievements = achievements;
            _notificationLogs = notificationLogs;
            _settings = settings;
            _hardModePenalties = hardModePenalties;
            _userMultipliers = userMultipliers;
            _planner = planner;
            _timeZoneService = timeZoneService;
            _navigation = navigation;
            _dialogs = dialogs;
            _popups = popups;
            _clock = clock;
            _backupFileStorage = backupFileStorage;
            _scheduledBackupConfigStore = scheduledBackupConfigStore;
            _scheduledBackupLogStore = scheduledBackupLogStore;
            _googleDriveBackupConnector = googleDriveBackupConnector;
            _scheduledBackupWorkScheduler = scheduledBackupWorkScheduler;
            _premiumSubscriptions = premiumSubscriptions ?? throw new ArgumentNullException(nameof(premiumSubscriptions));
            _watchSnapshots = watchSnapshots ?? throw new ArgumentNullException(nameof(watchSnapshots));
            _watchShortcuts = watchShortcuts ?? throw new ArgumentNullException(nameof(watchShortcuts));
            _activeCardNotificationAvailability = activeCardNotificationAvailability
                ?? throw new ArgumentNullException(nameof(activeCardNotificationAvailability));
            _pages = pages;
            _pageState = pageState;
            _cardWorkflow = cardWorkflow;
            _dashboardShortcuts = dashboardShortcuts;
            _getInitialization = getInitialization;
            _reconcileNotificationAsync = reconcileNotificationAsync
                ?? throw new ArgumentNullException(nameof(reconcileNotificationAsync));
            _refreshHomeAsync = refreshHomeAsync;
            _getRangeStart = getRangeStart;
            _getRangeEnd = getRangeEnd;
            _setRangeStart = setRangeStart;
            _setRangeEnd = setRangeEnd;
            _notifyPropertyChanged = notifyPropertyChanged;
        }

        public async Task FilterCardsByTagAsync()
        {
            var choice = await _dialogs.DisplayActionSheetAsync(
                "Add Card",
                "Cancel",
                null,
                _pageState.GetTags().ToArray());

            if (string.IsNullOrWhiteSpace(choice))
                return;

            _pageState.FilterCardsByTag(choice);
        }

        public async Task SearchCardsByTextAsync()
        {
            var input = await _dialogs.DisplayPromptAsync(
                "Search",
                "Filter Titles and Tags by:",
                accept: "OK",
                cancel: "Cancel",
                placeholder: "e.g. Education",
                keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(input))
                return;

            _pageState.FilterCardsBySearchTerm(input);
        }

        public async Task OpenAchievementsAsync()
        {
            await _navigation.PushAsync(
                new Points.Views.Achievements.AchievementsPage(
                    _cardWriter,
                    _achievements,
                    _pageState.GetTags(),
                    _navigation,
                    _dialogs,
                    _clock));
        }

        public async Task OpenDateRangePickerViewAsync()
        {
            await _navigation.PushAsync(
                new Points.Views.Shared.DateRangePickerPage(ApplyGlobalDateRangeAsync, _clock, _navigation, _dialogs));
        }

        public async Task OpenGoalViewAsync()
        {
            await _navigation.PushAsync(
                new Points.Views.Goals.GoalCreationPage(_cardReader, _goals, _clock, _navigation, _dialogs));
        }

        public async Task OpenSettingsAsync()
        {
            await _navigation.PushAsync(
                new Points.Views.Settings.SettingsPage(
                    _databaseMaintenance,
                    _databaseLifecycle,
                    _notificationLogs,
                    _settings,
                    _hardModePenalties,
                    _userMultipliers,
                    _navigation,
                    _dialogs,
                    _clock,
                    _timeZoneService,
                    _backupFileStorage,
                    _scheduledBackupConfigStore,
                    _scheduledBackupLogStore,
                    _googleDriveBackupConnector,
                    _scheduledBackupWorkScheduler,
                    _premiumSubscriptions,
                    _watchShortcuts,
                    _watchSnapshots,
                    _activeCardNotificationAvailability,
                    _reconcileNotificationAsync,
                    () => _refreshHomeAsync(_getRangeStart(), _getRangeEnd())));
        }

        public Task ReturnHomeAsync()
        {
            return _navigation.PopToRootAsync();
        }

        public async Task OpenMissedNotificationsLogAsync()
        {
            await _navigation.PushAsync(
                new Points.Views.Settings.NotificationLogPage(
                    _notificationLogs,
                    _clock,
                    _timeZoneService,
                    NotificationLogFilter.Missed));
        }

        public async Task OpenReportsAsync()
        {
            await _navigation.PushAsync(
                new Points.Views.Reports.ReportPage(_reports, _navigation, _clock));
        }

        public async Task OpenLeaderboardAsync()
        {
            var initialization = _getInitialization();
            if (initialization != null)
                await initialization;

            await _popups.ShowPopupAsync(
                new LeaderboardPopup(
                    new LeaderboardViewModel(
                        _cardReader,
                        _planner,
                        _clock,
                        _timeZoneService),
                    _dialogs));
        }

        public async Task OpenShortcutDetailsAsync(ShortcutModel? shortcut)
        {
            await _dashboardShortcuts.OpenShortcutDetailsAsync(shortcut);
        }

        public async Task OpenExistingCardAsync(ICardModel? model)
        {
            if (model == null)
                return;

            var page = _pageState.FindPageContaining(model);
            if (page == null)
                return;

            await _cardWorkflow.OpenDetailsForModelAsync(page, model);
        }

        private async Task ApplyGlobalDateRangeAsync(
            DateTime rangeStart,
            DateTime rangeEnd,
            bool followsCurrentDay)
        {
            var savedRange = GlobalVariables.SetRange(
                rangeStart,
                rangeEnd,
                _clock.LocalNow,
                followsCurrentDay);

            _setRangeStart(savedRange.Start);
            _setRangeEnd(savedRange.End);

            _notifyPropertyChanged(nameof(HomeViewModel.HeaderDate));
            _notifyPropertyChanged(nameof(HomeViewModel.GlobalValueColor));
            _notifyPropertyChanged(nameof(HomeViewModel.HasNegativeAvailableMission));

            await _refreshHomeAsync(savedRange.Start, savedRange.End);
        }
    }
}
