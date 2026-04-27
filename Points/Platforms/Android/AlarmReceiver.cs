#if ANDROID
using Android.App;
using Android.Content;
using Points.Helpers;
using Points.Services.Scheduling;

namespace Points.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public sealed class AlarmReceiver : BroadcastReceiver
    {
        public const string ActionAlarmFired = "POINTS.ALARM_FIRED";
        public const string ExtraScheduleId = "EXTRA_SCHEDULE_ID";

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action != ActionAlarmFired) return;

            var scheduleId = intent.GetLongExtra(ExtraScheduleId, -1);
            if (scheduleId <= 0) return;

            // OnReceive must return quickly; do async work in background task.
            _ = HandleAsync(scheduleId);
        }

        private static async Task HandleAsync(long scheduleId)
        {
            try
            {
                var coordinator = ServiceHelper.GetService<INotificationScheduleCoordinator>();
                await coordinator.HandleScheduleFiredAsync(scheduleId, DateTime.Now);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AlarmReceiver failed: {ex}");
            }
        }
    }
}
#endif
