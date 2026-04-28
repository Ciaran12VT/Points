#if ANDROID
using Android.App;
using Android.Content;
using Points.Helpers;
using Points.Services.Scheduling;

namespace Points.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = false, DirectBootAware = true)]
    [IntentFilter(new[]
    {
        "android.intent.action.BOOT_COMPLETED",
        "android.intent.action.LOCKED_BOOT_COMPLETED",
        "android.intent.action.TIME_SET",
        "android.intent.action.TIMEZONE_CHANGED",
        "android.intent.action.DATE_CHANGED",
        "android.app.action.SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED"
    })]
    public sealed class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var pendingResult = GoAsync();
            var action = intent.Action;

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleAsync(action);
                }
                finally
                {
                    pendingResult.Finish();
                }
            });
        }

        private static async Task HandleAsync(string? action)
        {
            try
            {
                var coordinator = ServiceHelper.GetService<INotificationScheduleCoordinator>();
                await coordinator.SyncEnabledSchedulesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BootReceiver failed to sync schedules after '{action}': {ex}");
            }
        }
    }
}
#endif
