using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Points.Helpers;
using Points.Interfaces;
using Points.Models;
using Points.Services;
using Points.Services.Achievements;
using Points.Services.Activity;
using Points.Services.Backup;
using Points.Services.Budgets;
using Points.Services.Cards;
using Points.Services.Goals;
using Points.Services.HardMode;
using Points.Services.Locks;
using Points.Services.MissionSharing;
using Points.Services.Missions;
using Points.Services.Multipliers;
using Points.Services.Navigation;
using Points.Services.Notifications;
using Points.Services.Planner;
using Points.Services.Premium;
using Points.Services.Reports;
using Points.Services.Schedules;
using Points.Services.Scheduling;
using Points.Services.Sc;
using Points.Services.Settings;
using Points.Services.Shortcuts;
using Points.Services.Sqlite;
using Points.Services.Persistence;
using Points.Services.Tat;
using Points.Services.Time;
using Points.Services.Trackers;
using Points.Services.Udmd;
using Points.Services.Watch;
using Points.ViewModels.Home;
using Points.Views.Home;

namespace Points
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

#if ANDROID
            builder.Services.AddSingleton<IAudioFeedback, AndroidAudioFeedback>();
            builder.Services.AddSingleton<IActiveCardNotificationService, Points.Platforms.Android.ActiveCardNotificationService>();
            builder.Services.AddSingleton<IDeviceAlarmScheduler, Points.Platforms.Android.AndroidDeviceAlarmScheduler>();
            builder.Services.AddSingleton<IScheduleNotificationPresenter, Points.Platforms.Android.AndroidScheduleNotificationPresenter>();
            builder.Services.AddSingleton<IScheduledBackupWorkScheduler, Points.Platforms.Android.AndroidScheduledBackupWorkScheduler>();
            builder.Services.AddSingleton<IWatchBridge, Points.Platforms.Android.AndroidWearBridge>();
#elif IOS
            builder.Services.AddSingleton<IAudioFeedback, NoopAudioFeedback>();
            builder.Services.AddSingleton<IActiveCardNotificationService, NullActiveCardNotificationService>();
            builder.Services.AddSingleton<IDeviceAlarmScheduler, NullDeviceAlarmScheduler>();
            builder.Services.AddSingleton<IScheduleNotificationPresenter, NullScheduleNotificationPresenter>();
            builder.Services.AddSingleton<IScheduledBackupWorkScheduler, Points.Platforms.iOS.IosScheduledBackupWorkScheduler>();
            builder.Services.AddSingleton<IWatchBridge, NoopWatchBridge>();
#elif WINDOWS
            builder.Services.AddSingleton<IAudioFeedback, NoopAudioFeedback>();
            builder.Services.AddSingleton<IActiveCardNotificationService, NullActiveCardNotificationService>();
            builder.Services.AddSingleton<IDeviceAlarmScheduler, NullDeviceAlarmScheduler>();
            builder.Services.AddSingleton<IScheduleNotificationPresenter, NullScheduleNotificationPresenter>();
            builder.Services.AddSingleton<IScheduledBackupWorkScheduler, Points.Platforms.Windows.WindowsScheduledBackupWorkScheduler>();
            builder.Services.AddSingleton<IWatchBridge, NoopWatchBridge>();
#else
            builder.Services.AddSingleton<IAudioFeedback, NoopAudioFeedback>();
            builder.Services.AddSingleton<IActiveCardNotificationService, NullActiveCardNotificationService>();
            builder.Services.AddSingleton<IDeviceAlarmScheduler, NullDeviceAlarmScheduler>();
            builder.Services.AddSingleton<IScheduleNotificationPresenter, NullScheduleNotificationPresenter>();
            builder.Services.AddSingleton<IScheduledBackupWorkScheduler, NullScheduledBackupWorkScheduler>();
            builder.Services.AddSingleton<IWatchBridge, NoopWatchBridge>();
#endif
            builder.Services.AddSingleton<IClock, SystemClock>();
            builder.Services.AddSingleton<ITimeZoneService, TimeZoneService>();
            builder.Services.AddSingleton<IAppNavigationService, MauiAppNavigationService>();
            builder.Services.AddSingleton<IAppDialogService, MauiDialogService>();
            builder.Services.AddSingleton<IAppPageService, MauiPageService>();
            builder.Services.AddSingleton<IPopupService, MauiPopupService>();
            builder.Services.AddSingleton<IActiveCardNotificationNavigationService, ActiveCardNotificationNavigationService>();
            builder.Services.AddSingleton<INotificationScheduleCoordinator, NotificationScheduleCoordinator>();
            builder.Services.AddSingleton<IBackupFileStorageService, BackupFileStorageService>();
            builder.Services.AddSingleton<IScheduledBackupConfigStore, JsonScheduledBackupConfigStore>();
            builder.Services.AddSingleton<IScheduledBackupLogStore, JsonLinesScheduledBackupLogStore>();
            builder.Services.AddSingleton<IScheduledBackupPackageCreator, BackupPackageScheduledBackupPackageCreator>();
            builder.Services.AddSingleton<IScheduledBackupLocalStorage, AppPrivateScheduledBackupLocalStorage>();
            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<IGoogleDriveOAuthClientConfigProvider, JsonGoogleDriveOAuthClientConfigProvider>();
            builder.Services.AddSingleton<IGoogleDriveTokenStore, SecureStorageGoogleDriveTokenStore>();
            builder.Services.AddSingleton<GoogleDriveBackupService>();
            builder.Services.AddSingleton<IGoogleDriveBackupConnector>(sp => sp.GetRequiredService<GoogleDriveBackupService>());
            builder.Services.AddSingleton<IScheduledBackupRemoteStorage>(sp => sp.GetRequiredService<GoogleDriveBackupService>());
            builder.Services.AddSingleton<IScheduledBackupRunner, ScheduledBackupRunner>();

            builder.Services.AddTransient<HomePage>();      // <-- add this
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddSingleton<AppShell>();      // <-- add this
            builder.Services.AddSingleton<SqliteDbService>();
            builder.Services.AddSingleton<IDatabaseInitializationService>(sp => sp.GetRequiredService<SqliteDbService>());
            builder.Services.AddSingleton<IDatabaseMaintenanceService>(sp => sp.GetRequiredService<SqliteDbService>());
            builder.Services.AddSingleton<ISqliteConnectionContext>(sp => sp.GetRequiredService<SqliteDbService>());
            builder.Services.AddSingleton<IAchievementService>(sp => new SqliteAchievementService(
                sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<ITimeZoneService>(),
                sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton<IBudgetService>(sp => new SqliteBudgetService(
    sp.GetRequiredService<ISqliteConnectionContext>(),
    sp.GetRequiredService<ITimeZoneService>()));
builder.Services.AddSingleton<ICardScheduleService, SqliteCardScheduleService>();
builder.Services.AddSingleton<IScCardService>(sp => new SqliteScCardService(
    sp.GetRequiredService<ISqliteConnectionContext>(),
    sp.GetRequiredService<ITimeZoneService>(),
    sp.GetRequiredService<ICardScheduleService>()));
builder.Services.AddSingleton<IMissionCardService>(sp => new SqliteMissionCardService(
    sp.GetRequiredService<ISqliteConnectionContext>(),
    sp.GetRequiredService<ITimeZoneService>()));
builder.Services.AddSingleton<ITrackerService>(sp => new SqliteTrackerService(
    sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<ITimeZoneService>(),
                sp.GetRequiredService<ICardScheduleService>()));
            builder.Services.AddSingleton<ITatCardService>(sp => new SqliteTatCardService(
                sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<ITimeZoneService>(),
                sp.GetRequiredService<ICardScheduleService>()));
            builder.Services.AddSingleton<IActivityService>(sp => new SqliteActivityService(
                sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<ITimeZoneService>()));
            builder.Services.AddSingleton<IHardModePenaltyService>(sp => new SqliteHardModePenaltyService(
                sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<IActivityService>(),
                sp.GetRequiredService<ITimeZoneService>()));
            builder.Services.AddSingleton<IUserMultiplierService, SqliteUserMultiplierService>();
            builder.Services.AddSingleton<IGoalService, SqliteGoalService>();
            builder.Services.AddSingleton<ILockService, SqliteLockService>();
            builder.Services.AddSingleton<SqliteCardService>(sp => new SqliteCardService(
                sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<ITimeZoneService>(),
                sp.GetRequiredService<ITatCardService>(),
                sp.GetRequiredService<IScCardService>(),
                sp.GetRequiredService<IMissionCardService>(),
                sp.GetRequiredService<IBudgetService>(),
                sp.GetRequiredService<IAchievementService>(),
                sp.GetRequiredService<ITrackerService>(),
                sp.GetRequiredService<ILockService>()));
            builder.Services.AddSingleton<ICardReadService>(sp => sp.GetRequiredService<SqliteCardService>());
            builder.Services.AddSingleton<ICardWriteService>(sp => sp.GetRequiredService<SqliteCardService>());
            builder.Services.AddSingleton<IPlannerCardSource>(sp => sp.GetRequiredService<SqliteCardService>());
            builder.Services.AddSingleton<INotificationLogService>(sp => new SqliteNotificationLogService(
                sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<ITimeZoneService>()));
            builder.Services.AddSingleton<IPlannerService>(sp => new SqlitePlannerService(
                sp.GetRequiredService<ISqliteConnectionContext>(),
                sp.GetRequiredService<IPlannerCardSource>(),
                sp.GetRequiredService<ITimeZoneService>(),
                sp.GetRequiredService<IClock>()));
            builder.Services.AddSingleton<IReportService, SqliteReportService>();
            builder.Services.AddSingleton<ISettingsService, SqliteSettingsService>();
            builder.Services.AddSingleton<IShortcutService, SqliteShortcutService>();
            builder.Services.AddSingleton<IUdmdService, SqliteUdmdService>();
            builder.Services.AddSingleton<IWatchShortcutSettingsService, WatchShortcutSettingsService>();
            builder.Services.AddSingleton<IWatchEventStore, SqliteWatchEventStore>();
            builder.Services.AddSingleton<IWatchSnapshotBuilder, WatchSnapshotBuilder>();
            builder.Services.AddSingleton<IWatchSnapshotPublishService, WatchSnapshotPublishService>();
            builder.Services.AddSingleton<IActiveCardChangeNotifier, ActiveCardChangeNotifier>();
            builder.Services.AddSingleton<IWatchCommandProcessor, WatchCommandProcessor>();
            builder.Services.AddSingleton<IMissionShareService, MissionShareService>();
            builder.Services.AddSingleton<IMissionShareLaunchHandler, MissionShareLaunchHandler>();
            builder.Services.AddSingleton<IPremiumSubscriptionService, HardcodedPremiumSubscriptionService>();

            var app = builder.Build();

            ServiceHelper.Services = app.Services;

            return app;
        }
    }

    public sealed class NoopAudioFeedback : IAudioFeedback
    {
        public void Tick() { }
        public void Thock() { }
        public void Clack() { }
    }

    public class NullDeviceAlarmScheduler : IDeviceAlarmScheduler
    {
        public Task CancelAsync(long scheduleId)
        {
            return Task.CompletedTask;
        }

        public Task ScheduleExactAsync(long scheduleId, DateTime scheduleFor, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    public class NullScheduleNotificationPresenter : IScheduleNotificationPresenter
    {
        public Task ShowScheduleFiredAsync(CardSchedule schedule, string? title, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
