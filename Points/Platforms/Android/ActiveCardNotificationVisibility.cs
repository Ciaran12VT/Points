#if ANDROID
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace Points.Platforms.Android;

internal static class ActiveCardNotificationVisibility
{
    public static bool IsVisible(
        Context context,
        string channelId,
        int notificationId)
    {
        if (!NotificationManagerCompat.From(context).AreNotificationsEnabled())
            return false;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
            && ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications)
                != Permission.Granted)
        {
            return false;
        }

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = manager?.GetNotificationChannel(channelId);
            if (channel == null || channel.Importance == NotificationImportance.None)
                return false;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.P
                && !string.IsNullOrWhiteSpace(channel.Group))
            {
                var group = manager?.GetNotificationChannelGroup(channel.Group);
                if (group?.IsBlocked == true)
                    return false;
            }
        }

        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            return true;

        try
        {
            var activeNotifications = manager?.GetActiveNotifications();
            return activeNotifications?.Any(
                       notification => notification.Id == notificationId) == true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Could not verify the active-card notification: {ex}");
            return false;
        }
    }
}
#endif
