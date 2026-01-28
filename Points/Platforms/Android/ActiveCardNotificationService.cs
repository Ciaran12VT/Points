#if ANDROID
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using AndroidX.Core.App;
using Points.Models;
using Points.Platforms.Android;
using Points.Services;
using System.Text.Json;
using aa = Android.App;

namespace Points.Platforms.Android
{
    public class ActiveCardNotificationService : IActiveCardNotificationService
    {
        public void UpdateActiveCardNotification(IActiveCardModel? cardModel)
        {
            var context = aa.Application.Context;

            if (cardModel is null)
            {
                // Stop the foreground service = remove persistent notification
                var stopIntent = new Intent(context, typeof(ActiveCardForegroundService));
                context.StopService(stopIntent);
            }
            else
            {
                // Start/update the foreground service with the new title
                var intent = new Intent(context, typeof(ActiveCardForegroundService));
                intent.PutExtra(ActiveCardForegroundService.ExtraCardJson, JsonSerializer.Serialize(new ActiveCardModelWrapper() { Type = cardModel.GetType().AssemblyQualifiedName, Data = JsonSerializer.SerializeToElement(cardModel, cardModel.GetType()) }));
                context.StartForegroundService(intent);
            }
        }

        // 🔔 IMPLEMENTED: debug notification fired from the HomePage button
        public void DebugBeep()
        {
            var context = aa.Application.Context;

            const string DebugChannelId = "points_debug_beep_channel";
            const string DebugChannelName = "Debug";
            const int DebugNotificationId = 9999;

            // 1) Ensure channel exists (Android 8+)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
                if (manager != null)
                {
                    var existing = manager.GetNotificationChannel(DebugChannelId);
                    if (existing == null)
                    {
                        var channel = new NotificationChannel(
                            DebugChannelId,
                            DebugChannelName,
                            NotificationImportance.High
                        )
                        {
                            Description = "Debug channel for testing sound/vibration.",
                            LockscreenVisibility = NotificationVisibility.Public
                        };

                        channel.EnableVibration(true);
                        channel.SetVibrationPattern(new long[] { 0, 500, 300, 500 });

                        var uri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
                        var audioAttrs = new AudioAttributes.Builder()
                            .SetUsage(AudioUsageKind.Notification)
                            .SetContentType(AudioContentType.Sonification)
                            .Build();
                        channel.SetSound(uri, audioAttrs);

                        manager.CreateNotificationChannel(channel);
                    }
                }
            }

            // 2) Build and show the notification
            var builder = new NotificationCompat.Builder(context, DebugChannelId)
                .SetContentTitle("Debug Beep")
                .SetContentText("If this is silent, Android is muting the app or channel.")
                .SetSmallIcon(Resource.Drawable.ic_m3_chip_close)   // make sure this exists
                .SetAutoCancel(true)
                .SetPriority((int)NotificationPriority.High)
                .SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate));

            var nm = NotificationManagerCompat.From(context);
            nm.Notify(DebugNotificationId, builder.Build());
        }
    }
}
#endif
