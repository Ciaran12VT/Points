#if ANDROID
using Android.App;
using Android.Content;
using Points.Helpers;
using Points.Services.Scheduling;

namespace Points.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = false, DirectBootAware = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.LOCKED_BOOT_COMPLETED" })]
    public sealed class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            _ = HandleAsync();
        }

        private static async Task HandleAsync()
        {
            try
            {
                var coordinator = ServiceHelper.GetService<INotificationScheduleCoordinator>();
                await coordinator.SyncEnabledSchedulesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BootReceiver failed to sync schedules: {ex}");
            }
        }
    }
}
#endif
