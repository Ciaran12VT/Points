#if ANDROID
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Points.Services.Notifications;
using AndroidSettings = Android.Provider.Settings;

namespace Points.Platforms.Android;

public sealed class AndroidActiveCardNotificationAvailabilityService
    : IActiveCardNotificationAvailabilityService
{
    public Task<ActiveCardNotificationAvailability> GetAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = global::Android.App.Application.Context;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            return Task.FromResult(new ActiveCardNotificationAvailability(
                ActiveCardNotificationAvailabilityStatus.PermissionDenied));
        }

        if (!NotificationManagerCompat.From(context).AreNotificationsEnabled())
        {
            return Task.FromResult(new ActiveCardNotificationAvailability(
                ActiveCardNotificationAvailabilityStatus.AppNotificationsDisabled));
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            var channel = manager?.GetNotificationChannel(ActiveCardForegroundService.NotificationChannelId);

            // A missing channel is creatable by the foreground service and is therefore available.
            if (channel?.Importance == NotificationImportance.None || IsChannelGroupBlocked(manager, channel))
            {
                return Task.FromResult(new ActiveCardNotificationAvailability(
                    ActiveCardNotificationAvailabilityStatus.ChannelDisabled));
            }
        }

        return Task.FromResult(ActiveCardNotificationAvailability.Available);
    }

    public async Task OpenNotificationSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = global::Android.App.Application.Context;
        var availability = await GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        Intent intent;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O &&
            availability.Status == ActiveCardNotificationAvailabilityStatus.ChannelDisabled)
        {
            intent = new Intent(AndroidSettings.ActionChannelNotificationSettings)
                .PutExtra(AndroidSettings.ExtraAppPackage, context.PackageName)
                .PutExtra(AndroidSettings.ExtraChannelId, ActiveCardForegroundService.NotificationChannelId);
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            intent = new Intent(AndroidSettings.ActionAppNotificationSettings)
                .PutExtra(AndroidSettings.ExtraAppPackage, context.PackageName);
        }
        else
        {
            intent = new Intent(AndroidSettings.ActionApplicationDetailsSettings)
                .SetData(global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
        }

        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

    private static bool IsChannelGroupBlocked(
        NotificationManager? manager,
        NotificationChannel? channel)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.P ||
            manager == null ||
            string.IsNullOrWhiteSpace(channel?.Group))
        {
            return false;
        }

        return manager.GetNotificationChannelGroup(channel.Group)?.IsBlocked == true;
    }
}
#endif
