using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Points.Helpers;
using Points.Interfaces;
using Points.Models;
using Points.Services;
using Points.Services.Scheduling;
using Points.Services.Sqlite;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;
using Points.Views;

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
#else
            builder.Services.AddSingleton<IAudioFeedback, NoopAudioFeedback>();
            builder.Services.AddSingleton<IActiveCardNotificationService, NullActiveCardNotificationService>();
            builder.Services.AddSingleton<IDeviceAlarmScheduler, NullDeviceAlarmScheduler>();
            builder.Services.AddSingleton<IScheduleNotificationPresenter, NullScheduleNotificationPresenter>();
#endif
            builder.Services.AddSingleton<IClock, SystemClock>();
            builder.Services.AddSingleton<ITimeZoneService, TimeZoneService>();
            builder.Services.AddSingleton<INotificationScheduleCoordinator, NotificationScheduleCoordinator>();

            builder.Services.AddTransient<HomePage>();      // <-- add this
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddSingleton<AppShell>();      // <-- add this
            builder.Services.AddSingleton<SqliteDbService>();
            builder.Services.AddSingleton<IDbService>(sp => sp.GetRequiredService<SqliteDbService>());
            builder.Services.AddSingleton<ISqliteConnectionContext>(sp => sp.GetRequiredService<SqliteDbService>());

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
