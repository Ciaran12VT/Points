using Points.Models;

namespace Points.Services.Scheduling
{
    public static class CardScheduleOccurrenceCalculator
    {
        /// <summary>
        /// Returns the next occurrence strictly after the supplied time, or null when the schedule has no future occurrence.
        /// </summary>
        public static DateTime? GetNextOccurrence(IScheduleModel schedule, DateTime now)
        {
            if (!schedule.IsEnabled)
                return null;

            now = WallClockScheduleTime.NormalizeLocal(now);
            var from = WallClockScheduleTime.NormalizeLocal(schedule.FromDateTime);
            var to = WallClockScheduleTime.NormalizeLocal(schedule.ToDateTime);

            if (to.HasValue && now > to.Value)
                return null;

            if (schedule.FrequencyType == FrequencyType.Once)
                return from > now ? from : null;

            var candidate = GetRecurringCandidate(schedule, from, now);

            if (candidate.HasValue && candidate.Value < from)
                candidate = from;

            if (candidate.HasValue && to.HasValue && candidate.Value > to.Value)
                return null;

            return candidate;
        }

        private static DateTime? GetRecurringCandidate(IScheduleModel schedule, DateTime anchor, DateTime now)
        {
            var timeOfDay = anchor.TimeOfDay;

            return schedule.FrequencyType switch
            {
                FrequencyType.EveryDays => NextEveryDays(schedule, anchor, now, timeOfDay),
                FrequencyType.EveryWeeks => NextSpecificWeekday(now, anchor.DayOfWeek, timeOfDay),
                FrequencyType.EveryMonths => NextMonthly(anchor, now, timeOfDay),
                FrequencyType.EveryYears => NextYearly(anchor, now, timeOfDay),
                FrequencyType.EveryWeekday => NextWeekday(now, timeOfDay),
                FrequencyType.EveryMonday => NextSpecificWeekday(now, DayOfWeek.Monday, timeOfDay),
                FrequencyType.EveryTuesday => NextSpecificWeekday(now, DayOfWeek.Tuesday, timeOfDay),
                FrequencyType.EveryWednesday => NextSpecificWeekday(now, DayOfWeek.Wednesday, timeOfDay),
                FrequencyType.EveryThursday => NextSpecificWeekday(now, DayOfWeek.Thursday, timeOfDay),
                FrequencyType.EveryFriday => NextSpecificWeekday(now, DayOfWeek.Friday, timeOfDay),
                FrequencyType.EverySaturday => NextSpecificWeekday(now, DayOfWeek.Saturday, timeOfDay),
                FrequencyType.EverySunday => NextSpecificWeekday(now, DayOfWeek.Sunday, timeOfDay),
                _ => null
            };
        }

        private static DateTime NextEveryDays(IScheduleModel schedule, DateTime anchor, DateTime now, TimeSpan timeOfDay)
        {
            var interval = Math.Max(1, schedule.FrequencyValue);

            if (now < anchor)
                return anchor;

            var daysSince = (now.Date - anchor.Date).Days;
            var occurrencesSinceAnchor = (int)Math.Floor(daysSince / (double)interval) + 1;
            var candidate = anchor.Date.AddDays(occurrencesSinceAnchor * interval).Add(timeOfDay);

            if (candidate <= now)
                candidate = candidate.AddDays(interval);

            return candidate;
        }

        private static DateTime NextSpecificWeekday(DateTime now, DayOfWeek target, TimeSpan timeOfDay)
        {
            var todayAtTime = now.Date.Add(timeOfDay);

            if (now.DayOfWeek == target && todayAtTime > now)
                return todayAtTime;

            var daysUntil = ((int)target - (int)now.DayOfWeek + 7) % 7;
            if (daysUntil == 0)
                daysUntil = 7;

            return now.Date.AddDays(daysUntil).Add(timeOfDay);
        }

        private static DateTime NextWeekday(DateTime now, TimeSpan timeOfDay)
        {
            var candidate = now.Date.Add(timeOfDay);
            if (candidate <= now)
                candidate = candidate.AddDays(1);

            while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                candidate = candidate.AddDays(1);

            return candidate;
        }

        private static DateTime NextMonthly(DateTime anchor, DateTime now, TimeSpan timeOfDay)
        {
            var targetDay = anchor.Day;
            var candidate = MakeSafeDate(now.Year, now.Month, targetDay).Add(timeOfDay);

            if (candidate <= now)
            {
                var nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
                candidate = MakeSafeDate(nextMonth.Year, nextMonth.Month, targetDay).Add(timeOfDay);
            }

            return candidate;
        }

        private static DateTime NextYearly(DateTime anchor, DateTime now, TimeSpan timeOfDay)
        {
            var candidate = MakeSafeDate(now.Year, anchor.Month, anchor.Day).Add(timeOfDay);

            if (candidate <= now)
                candidate = MakeSafeDate(now.Year + 1, anchor.Month, anchor.Day).Add(timeOfDay);

            return candidate;
        }

        private static DateTime MakeSafeDate(int year, int month, int day)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            return new DateTime(year, month, Math.Min(day, daysInMonth), 0, 0, 0, DateTimeKind.Unspecified);
        }
    }
}
