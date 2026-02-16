#if ANDROID
using Android.App;
using Android.Content;

namespace Points.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = false, DirectBootAware = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.LOCKED_BOOT_COMPLETED" })]
    public sealed class BootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            // TODO: load enabled schedules from DB and reschedule them
        }
    }
}
#endif
