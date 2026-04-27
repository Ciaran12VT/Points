#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Points.Services.Scheduling;
using aa = Android.App;

namespace Points.Platforms.Android
{
    public sealed class AndroidDeviceAlarmScheduler : IDeviceAlarmScheduler
    {
        private readonly Context _context;

        public AndroidDeviceAlarmScheduler()
        {
            _context = aa.Application.Context;
        }

        public Task ScheduleExactAsync(long scheduleId, DateTime scheduleFor, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return Task.FromCanceled(ct);

            var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
            if (alarmManager == null)
                return Task.CompletedTask;

            var pendingIntent = BuildPendingIntent(scheduleId);
            var triggerAtMillis = DateTimeToUnixMillis(scheduleFor);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
            else
            {
                alarmManager.SetExact(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }

            return Task.CompletedTask;
        }

        public Task CancelAsync(long scheduleId)
        {
            var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
            if (alarmManager == null)
                return Task.CompletedTask;

            var pendingIntent = BuildPendingIntent(scheduleId);
            alarmManager.Cancel(pendingIntent);
            pendingIntent.Cancel();

            return Task.CompletedTask;
        }

        private PendingIntent BuildPendingIntent(long scheduleId)
        {
            var intent = new Intent(_context, typeof(AlarmReceiver));
            intent.SetAction(AlarmReceiver.ActionAlarmFired);
            intent.PutExtra(AlarmReceiver.ExtraScheduleId, scheduleId);

            var requestCode = unchecked((int)scheduleId);

            return PendingIntent.GetBroadcast(
                _context,
                requestCode,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            )!;
        }

        private static long DateTimeToUnixMillis(DateTime dtLocal)
        {
            var utc = dtLocal.Kind == DateTimeKind.Utc ? dtLocal : dtLocal.ToUniversalTime();
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc - epoch).TotalMilliseconds;
        }
    }
}
#endif
