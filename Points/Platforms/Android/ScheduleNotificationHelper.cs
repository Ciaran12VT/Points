#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Points.Models;

namespace Points.Platforms.Android
{

    public static class ScheduleNotificationHelper
    {
        private const string ChannelId = "points_schedule_channel";
        private const string ChannelName = "Schedules";
        private const int BaseNotificationId = 4000;

        public static void EnsureChannel(Context context)
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

            var mgr = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            if (mgr == null) return;

            if (mgr.GetNotificationChannel(ChannelId) != null) return;

            var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.High)
            {
                Description = "Schedule notifications"
            };
            channel.EnableVibration(true);

            mgr.CreateNotificationChannel(channel);
        }

        public static void ShowScheduleFired(Context context, CardSchedule schedule, string? title)
        {
            EnsureChannel(context);

            var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName);
            launchIntent?.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var pending = PendingIntent.GetActivity(
                context,
                0,
                launchIntent!,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var header = $"Alarm: {(title ?? "N/A")}";
            var text = $"{schedule.Note}";

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(header)
                .SetContentText(text)
                .SetSmallIcon(Resource.Drawable.ic_m3_chip_close) // use any valid small icon
                .SetAutoCancel(true)
                .SetPriority((int)NotificationPriority.High)
                .SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate))
                .SetContentIntent(pending);

            NotificationManagerCompat.From(context)
                .Notify(BaseNotificationId + (int)(schedule.ScheduleId % 1000), builder.Build());
        }
    }
}
#endif
