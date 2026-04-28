#if ANDROID
using Android.App;
using Android.Content;
using Points.Helpers;
using Points.Services.Scheduling;
using Points.Services.Time;

namespace Points.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public sealed class AlarmReceiver : BroadcastReceiver
    {
        public const string ActionAlarmFired = "POINTS.ALARM_FIRED";
        public const string ExtraScheduleId = "EXTRA_SCHEDULE_ID";
        public const string ExtraScheduledForLocalTicks = "EXTRA_SCHEDULED_FOR_LOCAL_TICKS";

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action != ActionAlarmFired) return;

            var scheduleId = intent.GetLongExtra(ExtraScheduleId, -1);
            if (scheduleId <= 0) return;

            var scheduledForLocalTicks = intent.GetLongExtra(ExtraScheduledForLocalTicks, -1);

            var pendingResult = GoAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleAsync(scheduleId, scheduledForLocalTicks);
                }
                finally
                {
                    pendingResult.Finish();
                }
            });
        }

        private static async Task HandleAsync(long scheduleId, long scheduledForLocalTicks)
        {
            try
            {
                var coordinator = ServiceHelper.GetService<INotificationScheduleCoordinator>();
                var clock = ServiceHelper.GetService<IClock>();
                var firedAt = scheduledForLocalTicks > 0
                    ? new DateTime(scheduledForLocalTicks, DateTimeKind.Unspecified)
                    : clock.LocalNow;

                await coordinator.HandleScheduleFiredAsync(scheduleId, firedAt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AlarmReceiver failed: {ex}");
            }
        }
    }
}
#endif
