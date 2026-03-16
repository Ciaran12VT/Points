#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Points.Helpers;
using Points.Services.Sqlite.Interfaces;   // ServiceHelper.GetService<T>() if you use it

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
            _ = HandleAsync(context, scheduleId);
        }

        private static async Task HandleAsync(Context context, long scheduleId)
        {
            try
            {
                var db = ServiceHelper.GetService<IDbService>();
                var schedule = await db.GetCardScheduleByIdAsync(scheduleId);
                if (schedule == null) return;

                if (!schedule.IsEnabled) return;

                var title = await db.GetCardTitleByIdAsync(schedule.CardId);

                var now = DateTime.Now;

                // range checks
                if (now < schedule.FromDateTime) return;
                if (schedule.ToDateTime.HasValue && now > schedule.ToDateTime.Value) return;

                // ✅ Fire notification (example)
                ScheduleNotificationHelper.ShowScheduleFired(context, schedule, title);

                // ✅ Reschedule next occurrence
                var next = CardScheduleNextRunCalculator.GetNextOccurrence(schedule, now);
                if (next.HasValue)
                {
                    AndroidAlarmScheduler.ScheduleExact(context, schedule.ScheduleId, next.Value);
                }
                else
                {
                    // No next occurrence => stop
                    AndroidAlarmScheduler.Cancel(context, schedule.ScheduleId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AlarmReceiver failed: {ex}");
            }
        }
    }
}
#endif
