using Points.Models;
using Points.Services.Scheduling;
using System.Globalization;

namespace Points.Services.Locks
{
    public static class LockEvaluator
    {
        public static bool IsLockedNow(
            IActiveCardModel card,
            DateTime now,
            IEnumerable<IActiveCardModel> activeCardModels,
            out DateTime availableAt)
        {
            availableAt = default;
            var localNow = ToLocalWallClock(now);

            if (card.Locks == null || card.Locks.Count == 0)
                return false;

            // For fast dependency lookup
            var byId = activeCardModels
                .GroupBy(c => c.CardID)
                .ToDictionary(g => g.Key, g => g.First());

            DateTime? furthestUnlock = null;
            var anyLocked = false;

            foreach (var l in card.Locks)
            {
                if (!LockAppliesNow(l, localNow, byId, out var thisUnlock))
                    continue;

                anyLocked = true;

                if (furthestUnlock == null || thisUnlock > furthestUnlock.Value)
                    furthestUnlock = thisUnlock;
            }

            if (!anyLocked || furthestUnlock == null)
                return false;

            availableAt = furthestUnlock.Value;
            return availableAt > localNow;
        }

        public static string FormatRemaining(DateTime now, DateTime availableAt)
        {
            var localNow = ToLocalWallClock(now);
            var localAvailableAt = ToLocalWallClock(availableAt);

            var remaining = localAvailableAt - localNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            if (localAvailableAt.Date == localNow.Date)
                return $"{remaining.TotalHours:0.0} hrs";

            return $"{remaining.TotalDays:0.0} days";
        }

        private static DateTime ToLocalWallClock(DateTime value)
        {
            return WallClockScheduleTime.NormalizeLocal(value);
        }

        // -----------------------------
        // Core evaluation
        // -----------------------------

        private static bool LockAppliesNow(
            LockModel l,
            DateTime now,
            Dictionary<long, IActiveCardModel> activeCardsById,
            out DateTime availableAt)
        {
            availableAt = default;

            // 1) Schedule gate: if no schedules, treat as "always scheduled"
            var scheduleApplies = SchedulesApplyNow(l.Schedules, now);
            if (!scheduleApplies)
                return false;

            // 2) Time window gate
            if (!IsWithinTimeWindow(now, l.TimeWindowStart, l.TimeWindowEnd, out var windowEnd))
                return false;

            // 3) Dependency gate: if no deps => lock applies
            if (l.Dependencies == null || l.Dependencies.Count == 0)
            {
                availableAt = windowEnd;
                return true;
            }

            // If ALL dependencies are satisfied, lock does NOT apply.
            // If ANY dependency is not satisfied (or missing card), lock applies.
            var allDepsSatisfied = true;

            foreach (var dep in l.Dependencies)
            {
                if (!activeCardsById.TryGetValue(dep.TaskDependencyCardId, out var depCard))
                {
                    allDepsSatisfied = false;
                    break;
                }

                if (!IsDependencySatisfied(depCard, dep, now))
                {
                    allDepsSatisfied = false;
                    break;
                }
            }

            if (allDepsSatisfied)
                return false;

            // deps NOT satisfied => lock applies until end of time window
            availableAt = windowEnd;
            return true;
        }

        // -----------------------------
        // Schedules
        // -----------------------------

        private static bool SchedulesApplyNow(List<LockScheduleModel>? schedules, DateTime now)
        {
            if (schedules == null || schedules.Count == 0)
                return true;

            foreach (var s in schedules)
            {
                if (s.IsEnabled && ScheduleAppliesNow(s, now))
                    return true;
            }

            return false;
        }

        private static bool ScheduleAppliesNow(LockScheduleModel s, DateTime now)
        {
            // ToDateTime is optional; if null, treat as "no end"
            var from = ToLocalWallClock(s.FromDateTime);
            var to = s.ToDateTime.HasValue ? ToLocalWallClock(s.ToDateTime.Value) : (DateTime?)null;

            // If schedule hasn't started yet, it can't apply
            if (now < from)
                return false;

            if (to.HasValue && now > to.Value)
                return false;

            var freq = Math.Max(1, s.FrequencyValue);

            switch (s.FrequencyType)
            {
                case FrequencyType.Once:
                    // If ToDateTime is null, treat as "from instant onward"
                    return to.HasValue ? (now >= from && now <= to.Value) : (now >= from);

                case FrequencyType.EveryDays:
                    return MatchesEveryNDays(from.Date, to?.Date, now.Date, freq);

                case FrequencyType.EveryWeeks:
                    // Applies only on the same DayOfWeek as FromDateTime,
                    // every N weeks starting from FromDateTime.
                    if (now.DayOfWeek != from.DayOfWeek) return false;
                    return MatchesEveryNWeeks(from.Date, to?.Date, now.Date, freq);

                case FrequencyType.EveryMonths:
                    // Applies only on same day-of-month as FromDateTime,
                    // every N months starting from FromDateTime.
                    if (now.Day != from.Day) return false;
                    return MatchesEveryNMonths(from.Date, to?.Date, now.Date, freq);

                case FrequencyType.EveryYears:
                    // Applies only on same month/day as FromDateTime,
                    // every N years starting from FromDateTime.
                    if (now.Month != from.Month || now.Day != from.Day) return false;
                    return MatchesEveryNYears(from.Date, to?.Date, now.Date, freq);

                case FrequencyType.EveryMonday: return now.DayOfWeek == DayOfWeek.Monday;
                case FrequencyType.EveryTuesday: return now.DayOfWeek == DayOfWeek.Tuesday;
                case FrequencyType.EveryWednesday: return now.DayOfWeek == DayOfWeek.Wednesday;
                case FrequencyType.EveryThursday: return now.DayOfWeek == DayOfWeek.Thursday;
                case FrequencyType.EveryFriday: return now.DayOfWeek == DayOfWeek.Friday;
                case FrequencyType.EverySaturday: return now.DayOfWeek == DayOfWeek.Saturday;
                case FrequencyType.EverySunday: return now.DayOfWeek == DayOfWeek.Sunday;

                case FrequencyType.EveryWeekday:
                    return now.DayOfWeek is DayOfWeek.Monday
                        or DayOfWeek.Tuesday
                        or DayOfWeek.Wednesday
                        or DayOfWeek.Thursday
                        or DayOfWeek.Friday;

                default:
                    // safest: if unknown schedule type, don't apply it
                    return false;
            }
        }

        private static bool MatchesEveryNDays(DateTime fromDate, DateTime? toDate, DateTime today, int n)
        {
            if (today < fromDate) return false;
            if (toDate.HasValue && today > toDate.Value) return false;

            var deltaDays = (today - fromDate).Days;
            return (deltaDays % n) == 0;
        }

        private static bool MatchesEveryNWeeks(DateTime fromDate, DateTime? toDate, DateTime today, int n)
        {
            if (today < fromDate) return false;
            if (toDate.HasValue && today > toDate.Value) return false;

            var deltaDays = (today - fromDate).Days;
            var deltaWeeks = deltaDays / 7;
            return (deltaWeeks % n) == 0;
        }

        private static bool MatchesEveryNMonths(DateTime fromDate, DateTime? toDate, DateTime today, int n)
        {
            if (today < fromDate) return false;
            if (toDate.HasValue && today > toDate.Value) return false;

            var monthsFrom = fromDate.Year * 12 + fromDate.Month;
            var monthsToday = today.Year * 12 + today.Month;
            var deltaMonths = monthsToday - monthsFrom;

            if (deltaMonths < 0) return false;
            return (deltaMonths % n) == 0;
        }

        private static bool MatchesEveryNYears(DateTime fromDate, DateTime? toDate, DateTime today, int n)
        {
            if (today < fromDate) return false;
            if (toDate.HasValue && today > toDate.Value) return false;

            var deltaYears = today.Year - fromDate.Year;
            if (deltaYears < 0) return false;
            return (deltaYears % n) == 0;
        }

        // -----------------------------
        // Time window (supports crossing midnight)
        // -----------------------------

        private static bool IsWithinTimeWindow(DateTime now, TimeOnly start, TimeOnly end, out DateTime windowEnd)
        {
            var startDt = now.Date + start.ToTimeSpan();
            var endDt = now.Date + end.ToTimeSpan();

            // If end < start, treat as crossing midnight (e.g. 22:00 -> 02:00)
            if (endDt < startDt)
                endDt = endDt.AddDays(1);

            windowEnd = endDt;

            // If the window crosses midnight, "now" might also be after midnight.
            // Example: now at 01:00, start=22:00, end=02:00 => startDt is today 22:00 (wrong day)
            // Fix: if window crosses midnight AND now is before end time-of-day,
            // shift startDt back one day.
            if (end.ToTimeSpan() < start.ToTimeSpan() && now.TimeOfDay <= end.ToTimeSpan())
            {
                startDt = startDt.AddDays(-1);
                windowEnd = endDt; // already next-day end
            }

            return now >= startDt && now <= windowEnd;
        }

        // -----------------------------
        // Dependencies
        // -----------------------------

        private static bool IsDependencySatisfied(IActiveCardModel depCard, LockTaskDependencyModel dep, DateTime now)
        {
            var tsr = new TimeScopeRange(dep.TimeScope, now);

            double actual;

            switch (dep.MetricType)
            {
                case LockDependencyMetricType.ActiveTime:
                    // GetActiveTime returns TimeSpan; goal is in hours
                    actual = depCard.GetActiveTime(tsr.Start, tsr.End).TotalHours;
                    break;

                case LockDependencyMetricType.Points:
                    actual = MultiplierValueCalculator.GetValue(depCard, tsr.Start, tsr.End);
                    break;

                default:
                    return false;
            }

            return dep.TargetValence switch
            {
                TargetValence.MustBeGreaterThan => actual > dep.TargetValue,
                TargetValence.MustBeLessThan => actual < dep.TargetValue,
                _ => false
            };
        }
    }
}
