using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Points.Interfaces;
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
#else
            builder.Services.AddSingleton<IAudioFeedback, NoopAudioFeedback>();
#endif

            builder.Services.AddTransient<HomePage>();      // <-- add this
            builder.Services.AddSingleton<AppShell>();      // <-- add this

            return builder.Build();
        }
    }

    public sealed class NoopAudioFeedback : IAudioFeedback
    {
        public void Tick() { }
        public void Thock() { }
        public void Clack() { }
    }
}
