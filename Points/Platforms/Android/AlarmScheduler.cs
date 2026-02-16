#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Java.Util;
using Points.Models;
using Points.Services;
using System.Globalization;
using aa = Android.App;

namespace Points.Platforms.Android
{
    public sealed class AlarmScheduler : IAlarmScheduler
    {
        private readonly Context _context;

        public AlarmScheduler()
        {
            _context = aa.Application.Context;
        }

        public Task ScheduleAllAsync(IEnumerable<CardSchedule> schedules, CancellationToken ct = default)
        {
            foreach (var s in schedules)
            {
                if (ct.IsCancellationRequested) break;
                _ = ScheduleOneAsync(s, ct);
            }
            return Task.CompletedTask;
        }

        public Task ScheduleOneAsync(CardSchedule schedule, CancellationToken ct = default)
        {
            // Respect enable/disable
            if (!schedule.IsEnabled)
                return CancelOneAsync(schedule.ScheduleId);

            // Compute next time (local time; use your policy consistently)
            var now = DateTime.Now;
            var next = ScheduleCalculator.TryGetNextOccurrence(schedule, now);

            if (next is null)
            {
                // Nothing more to schedule (e.g. past ToDateTime)
                return CancelOneAsync(schedule.ScheduleId);
            }

            var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
            if (alarmManager == null) return Task.CompletedTask;

            var pendingIntent = BuildPendingIntent(schedule.ScheduleId);

            // Use epoch millis
            long triggerAtMillis = DateTimeToUnixMillis(next.Value);

            // Android 12+ exact alarm restrictions (still schedules, but might be inexact if not allowed)
            // Your app can declare SCHEDULE_EXACT_ALARM; user may still need to allow it on some OEMs.
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

        public Task CancelOneAsync(long scheduleId)
        {
            var alarmManager = (AlarmManager?)_context.GetSystemService(Context.AlarmService);
            if (alarmManager == null) return Task.CompletedTask;

            var pi = BuildPendingIntent(scheduleId);
            alarmManager.Cancel(pi);
            pi.Cancel();
            return Task.CompletedTask;
        }

        public Task CancelAllAsync(IEnumerable<long> scheduleIds)
        {
            foreach (var id in scheduleIds)
                _ = CancelOneAsync(id);

            return Task.CompletedTask;
        }

        private PendingIntent BuildPendingIntent(long scheduleId)
        {
            var intent = new Intent(_context, typeof(AlarmReceiver));
            intent.SetAction(AlarmReceiver.ActionAlarmFired);
            intent.PutExtra(AlarmReceiver.ExtraScheduleId, scheduleId);

            // requestCode MUST be stable and unique per schedule -> use ScheduleId (fits int range? cast carefully)
            // If your ScheduleId can exceed int.MaxValue, hash it.
            int requestCode = unchecked((int)scheduleId);

            return PendingIntent.GetBroadcast(
                _context,
                requestCode,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            )!;
        }

        private static long DateTimeToUnixMillis(DateTime dtLocal)
        {
            // Use local -> UTC epoch millis
            var utc = dtLocal.Kind == DateTimeKind.Utc ? dtLocal : dtLocal.ToUniversalTime();
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc - epoch).TotalMilliseconds;
        }
    }

    internal static class ScheduleCalculator
    {
        /// <summary>
        /// Returns the next time this schedule should fire strictly AFTER 'now'.
        /// Returns null if no more occurrences exist (past ToDateTime, etc).
        /// </summary>
        public static DateTime? TryGetNextOccurrence(CardSchedule s, DateTime now)
        {
            // Disabled handled by caller
            // If we have a ToDateTime, and it's already past, nothing to do.
            if (s.ToDateTime.HasValue && now > s.ToDateTime.Value)
                return null;

            // Helper to validate bounds
            static DateTime? ClampToRange(CardSchedule s, DateTime candidate)
            {
                if (candidate < s.FromDateTime)
                    candidate = s.FromDateTime;

                if (s.ToDateTime.HasValue && candidate > s.ToDateTime.Value)
                    return null;

                return candidate;
            }

            // “Once”
            if (s.FrequencyType == FrequencyType.Once)
            {
                if (s.FromDateTime > now) return s.FromDateTime;
                return null;
            }

            // For all recurring types, we want the next time >= now+epsilon
            var baseTime = s.FromDateTime;

            // Normalize: if schedule starts in future, next is start (or next matching weekday)
            if (now < baseTime)
            {
                return NextForTypeFromAnchor(s, baseTime, now);
            }

            // Otherwise compute from now
            return NextForTypeFromAnchor(s, baseTime, now);
        }

        private static DateTime? NextForTypeFromAnchor(CardSchedule s, DateTime anchor, DateTime now)
        {
            // keep the time-of-day from FromDateTime
            var timeOfDay = anchor.TimeOfDay;

            DateTime candidate;

            switch (s.FrequencyType)
            {
                case FrequencyType.EveryDays:
                    {
                        var n = Math.Max(1, s.FrequencyValue);
                        // find days since anchor date
                        var daysSince = (now.Date - anchor.Date).Days;
                        var k = (int)Math.Floor(daysSince / (double)n) + 1;
                        candidate = anchor.Date.AddDays(k * n).Add(timeOfDay);

                        // If we're still before the anchor (can happen with weird inputs), bump
                        if (candidate <= now) candidate = candidate.AddDays(n);

                        return InRange(s, candidate);
                    }

                case FrequencyType.EveryWeeks:
                    {
                        // weekly on same weekday/time as anchor
                        candidate = NextWeekly(anchor, now, timeOfDay);
                        return InRange(s, candidate);
                    }

                case FrequencyType.EveryMonths:
                    {
                        candidate = NextMonthly(anchor, now, timeOfDay);
                        return InRange(s, candidate);
                    }

                case FrequencyType.EveryYears:
                    {
                        candidate = NextYearly(anchor, now, timeOfDay);
                        return InRange(s, candidate);
                    }

                case FrequencyType.EveryWeekday:
                    {
                        candidate = NextWeekday(now, timeOfDay);
                        return InRange(s, candidate);
                    }

                case FrequencyType.EveryMonday:
                case FrequencyType.EveryTuesday:
                case FrequencyType.EveryWednesday:
                case FrequencyType.EveryThursday:
                case FrequencyType.EveryFriday:
                case FrequencyType.EverySaturday:
                case FrequencyType.EverySunday:
                    {
                        var target = ToDayOfWeek(s.FrequencyType);
                        candidate = NextSpecificWeekday(now, target, timeOfDay);
                        return InRange(s, candidate);
                    }

                default:
                    return null;
            }
        }

        private static DateTime? InRange(CardSchedule s, DateTime candidate)
        {
            // must be >= FromDateTime
            if (candidate < s.FromDateTime)
                candidate = s.FromDateTime;

            if (s.ToDateTime.HasValue && candidate > s.ToDateTime.Value)
                return null;

            return candidate;
        }

        private static DayOfWeek ToDayOfWeek(FrequencyType ft) => ft switch
        {
            FrequencyType.EveryMonday => DayOfWeek.Monday,
            FrequencyType.EveryTuesday => DayOfWeek.Tuesday,
            FrequencyType.EveryWednesday => DayOfWeek.Wednesday,
            FrequencyType.EveryThursday => DayOfWeek.Thursday,
            FrequencyType.EveryFriday => DayOfWeek.Friday,
            FrequencyType.EverySaturday => DayOfWeek.Saturday,
            FrequencyType.EverySunday => DayOfWeek.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(ft))
        };

        private static DateTime NextSpecificWeekday(DateTime now, DayOfWeek target, TimeSpan timeOfDay)
        {
            var todayAtTime = now.Date.Add(timeOfDay);

            // If today is target and still in future, use today
            if (now.DayOfWeek == target && todayAtTime > now)
                return todayAtTime;

            // otherwise find next target day
            int daysUntil = ((int)target - (int)now.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7;

            return now.Date.AddDays(daysUntil).Add(timeOfDay);
        }

        private static DateTime NextWeekday(DateTime now, TimeSpan timeOfDay)
        {
            var candidate = now.Date.Add(timeOfDay);
            if (candidate <= now) candidate = candidate.AddDays(1);

            while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                candidate = candidate.AddDays(1);

            return candidate;
        }

        private static DateTime NextWeekly(DateTime anchor, DateTime now, TimeSpan timeOfDay)
        {
            // next occurrence on anchor weekday/time
            var target = anchor.DayOfWeek;
            return NextSpecificWeekday(now, target, timeOfDay);
        }

        private static DateTime NextMonthly(DateTime anchor, DateTime now, TimeSpan timeOfDay)
        {
            // "same day-of-month as anchor" (clamp if shorter month)
            int targetDay = anchor.Day;

            var year = now.Year;
            var month = now.Month;

            DateTime candidate = MakeSafeDate(year, month, targetDay).Add(timeOfDay);
            if (candidate <= now)
            {
                // move to next month
                var nextMonth = new DateTime(year, month, 1).AddMonths(1);
                candidate = MakeSafeDate(nextMonth.Year, nextMonth.Month, targetDay).Add(timeOfDay);
            }

            return candidate;
        }

        private static DateTime NextYearly(DateTime anchor, DateTime now, TimeSpan timeOfDay)
        {
            int targetMonth = anchor.Month;
            int targetDay = anchor.Day;

            DateTime candidate = MakeSafeDate(now.Year, targetMonth, targetDay).Add(timeOfDay);
            if (candidate <= now)
                candidate = MakeSafeDate(now.Year + 1, targetMonth, targetDay).Add(timeOfDay);

            return candidate;
        }

        private static DateTime MakeSafeDate(int year, int month, int day)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var safeDay = Math.Min(day, daysInMonth);
            return new DateTime(year, month, safeDay);
        }
    }
}
#endif
