#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Points.Models;

namespace Points.Platforms.Android
{
    public static class AndroidAlarmScheduler
    {
        public static void ScheduleExact(Context context, long scheduleId, DateTime whenLocal)
        {
            var alarmMgr = (AlarmManager?)context.GetSystemService(Context.AlarmService);
            if (alarmMgr == null) return;

            var intent = new Intent(context, typeof(AlarmReceiver));
            intent.PutExtra(AlarmReceiver.ExtraScheduleId, scheduleId);

            // requestCode must be stable per scheduleId
            var pending = PendingIntent.GetBroadcast(
                context,
                requestCode: (int)(scheduleId % int.MaxValue),
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var triggerAtMillis = new DateTimeOffset(whenLocal).ToUnixTimeMilliseconds();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                alarmMgr.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
            }
            else
            {
                alarmMgr.SetExact(AlarmType.RtcWakeup, triggerAtMillis, pending);
            }
        }

        public static void Cancel(Context context, long scheduleId)
        {
            var alarmMgr = (AlarmManager?)context.GetSystemService(Context.AlarmService);
            if (alarmMgr == null) return;

            var intent = new Intent(context, typeof(AlarmReceiver));

            var pending = PendingIntent.GetBroadcast(
                context,
                requestCode: (int)(scheduleId % int.MaxValue),
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            alarmMgr.Cancel(pending);
            pending.Cancel();
        }
    }

    public static class CardScheduleNextRunCalculator
    {
        public static DateTime? GetNextOccurrence(CardSchedule s, DateTime nowLocal)
        {
            if (!s.IsEnabled) return null;

            // If now is before start, next is start.
            var cursor = nowLocal < s.FromDateTime ? s.FromDateTime : nowLocal;

            DateTime? next = s.FrequencyType switch
            {
                FrequencyType.Once => s.FromDateTime > nowLocal ? s.FromDateTime : (DateTime?)null,

                FrequencyType.EveryDays => NextEveryNDays(s, cursor),

                FrequencyType.EveryWeeks => NextEveryWeeks(s, cursor),

                FrequencyType.EveryMonths => NextEveryMonths(s, cursor),

                FrequencyType.EveryYears => NextEveryYears(s, cursor),

                FrequencyType.EveryWeekday => NextWeekday(s, cursor),

                FrequencyType.EveryMonday => NextSpecificDay(s, cursor, DayOfWeek.Monday),
                FrequencyType.EveryTuesday => NextSpecificDay(s, cursor, DayOfWeek.Tuesday),
                FrequencyType.EveryWednesday => NextSpecificDay(s, cursor, DayOfWeek.Wednesday),
                FrequencyType.EveryThursday => NextSpecificDay(s, cursor, DayOfWeek.Thursday),
                FrequencyType.EveryFriday => NextSpecificDay(s, cursor, DayOfWeek.Friday),
                FrequencyType.EverySaturday => NextSpecificDay(s, cursor, DayOfWeek.Saturday),
                FrequencyType.EverySunday => NextSpecificDay(s, cursor, DayOfWeek.Sunday),

                _ => null
            };

            if (next.HasValue)
            {
                // clamp to ToDateTime if set
                if (s.ToDateTime.HasValue && next.Value > s.ToDateTime.Value)
                    return null;

                // also never schedule before FromDateTime
                if (next.Value < s.FromDateTime)
                    return s.FromDateTime;
            }

            return next;
        }

        private static DateTime? NextEveryNDays(CardSchedule s, DateTime cursor)
        {
            var n = Math.Max(1, s.FrequencyValue);
            var start = s.FromDateTime;

            if (cursor < start) return start;

            // step forward by N days from start until strictly > now
            var daysSince = (cursor.Date - start.Date).Days;
            var k = (daysSince / n) + 1;

            var nextDate = start.Date.AddDays(k * n).Add(start.TimeOfDay);
            return nextDate;
        }

        private static DateTime? NextEveryWeeks(CardSchedule s, DateTime cursor)
        {
            var start = s.FromDateTime;
            if (cursor < start) return start;

            // same weekday/time as start, weekly
            var next = start;
            while (next <= cursor)
                next = next.AddDays(7);

            return next;
        }

        private static DateTime? NextEveryMonths(CardSchedule s, DateTime cursor)
        {
            var start = s.FromDateTime;
            if (cursor < start) return start;

            var next = start;
            while (next <= cursor)
                next = next.AddMonths(1);

            return next;
        }

        private static DateTime? NextEveryYears(CardSchedule s, DateTime cursor)
        {
            var start = s.FromDateTime;
            if (cursor < start) return start;

            var next = start;
            while (next <= cursor)
                next = next.AddYears(1);

            return next;
        }

        private static DateTime? NextWeekday(CardSchedule s, DateTime cursor)
        {
            var start = s.FromDateTime;
            var time = start.TimeOfDay;

            var d = cursor.Date;
            for (int i = 0; i < 14; i++)
            {
                d = d.AddDays(1);
                if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

                var candidate = d.Add(time);
                if (candidate < start) continue;
                if (candidate > cursor) return candidate;
            }
            return null;
        }

        private static DateTime? NextSpecificDay(CardSchedule s, DateTime cursor, DayOfWeek day)
        {
            var start = s.FromDateTime;
            var time = start.TimeOfDay;

            var d = cursor.Date;
            for (int i = 0; i < 14; i++)
            {
                d = d.AddDays(1);
                if (d.DayOfWeek != day) continue;

                var candidate = d.Add(time);
                if (candidate < start) continue;
                if (candidate > cursor) return candidate;
            }
            return null;
        }
    }

}
#endif
