using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Points.Helpers;
using Points.Interfaces;
using Points.Models;
using Points.Services;
using Points.Services.Sqlite;
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
            builder.Services.AddSingleton<IAlarmScheduler, Points.Platforms.Android.AlarmScheduler>();
#else
            builder.Services.AddSingleton<IAudioFeedback, NoopAudioFeedback>();
            builder.Services.AddSingleton<IActiveCardNotificationService, NullActiveCardNotificationService>();
            builder.Services.AddSingleton<IAlarmScheduler, NullAlarmScheduler>();
#endif

            builder.Services.AddTransient<HomePage>();      // <-- add this
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddSingleton<AppShell>();      // <-- add this
            builder.Services.AddSingleton<IDbService, SqliteDbService>();

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

    public class NullAlarmScheduler : IAlarmScheduler
    {
        public Task CancelAllAsync(IEnumerable<long> scheduleIds)
        {
            throw new NotImplementedException();
        }

        public Task CancelOneAsync(long scheduleId)
        {
            throw new NotImplementedException();
        }

        public Task ScheduleAllAsync(IEnumerable<CardSchedule> schedules, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task ScheduleOneAsync(CardSchedule schedule, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
