#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Points.Services.Scheduling;
using Points.Services.Time;
using aa = Android.App;

namespace Points.Platforms.Android
{
    public sealed class AndroidDeviceAlarmScheduler : IDeviceAlarmScheduler
    {
        private readonly Context _context;
        private readonly ITimeZoneService _timeZoneService;

        public AndroidDeviceAlarmScheduler(ITimeZoneService timeZoneService)
        {
            _context = aa.Application.Context;
            _timeZoneService = timeZoneService;
        }

        public Task ScheduleExactAsync(long scheduleId, DateTime scheduleFor, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return Task.FromCanceled(ct);

            var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
            if (alarmManager == null)
                return Task.CompletedTask;

            var scheduleForLocal = WallClockScheduleTime.NormalizeLocal(scheduleFor);
            var pendingIntent = BuildPendingIntent(scheduleId, scheduleForLocal);
            var triggerAtMillis = WallClockScheduleTime.ToUnixTimeMilliseconds(scheduleForLocal, _timeZoneService);

            ScheduleAlarm(alarmManager, triggerAtMillis, pendingIntent);

            return Task.CompletedTask;
        }

        private static void ScheduleAlarm(AlarmManager alarmManager, long triggerAtMillis, PendingIntent pendingIntent)
        {
            try
            {
                if (CanUseExactAlarms(alarmManager))
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                    {
                        alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
                    }
                    else
                    {
                        alarmManager.SetExact(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
                    }

                    return;
                }
            }
            catch (Java.Lang.SecurityException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exact alarm permission denied; scheduling inexact alarm instead: {ex}");
            }

            ScheduleInexactAlarm(alarmManager, triggerAtMillis, pendingIntent);
        }

        private static bool CanUseExactAlarms(AlarmManager alarmManager)
        {
            return Build.VERSION.SdkInt < BuildVersionCodes.S || alarmManager.CanScheduleExactAlarms();
        }

        private static void ScheduleInexactAlarm(AlarmManager alarmManager, long triggerAtMillis, PendingIntent pendingIntent)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
            else
            {
                alarmManager.Set(AlarmType.RtcWakeup, triggerAtMillis, pendingIntent);
            }
        }

        public Task CancelAsync(long scheduleId)
        {
            var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
            if (alarmManager == null)
                return Task.CompletedTask;

            var pendingIntent = BuildPendingIntent(scheduleId, null);
            alarmManager.Cancel(pendingIntent);
            pendingIntent.Cancel();

            return Task.CompletedTask;
        }

        private PendingIntent BuildPendingIntent(long scheduleId, DateTime? scheduleForLocal)
        {
            var intent = new Intent(_context, typeof(AlarmReceiver));
            intent.SetAction(AlarmReceiver.ActionAlarmFired);
            intent.PutExtra(AlarmReceiver.ExtraScheduleId, scheduleId);

            if (scheduleForLocal.HasValue)
                intent.PutExtra(AlarmReceiver.ExtraScheduledForLocalTicks, WallClockScheduleTime.NormalizeLocal(scheduleForLocal.Value).Ticks);

            var requestCode = unchecked((int)scheduleId);

            return PendingIntent.GetBroadcast(
                _context,
                requestCode,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            )!;
        }

    }
}
#endif
