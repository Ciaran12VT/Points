using Points.Models;

namespace Points.Services.Activity
{
    public static class ActivityIntervalCalculator
    {
        public static TimeSpan GetActiveTimeInRange(
            IEnumerable<ActivityModel>? activity,
            DateTime start,
            DateTime end,
            DateTime utcNow)
        {
            var startUtc = ActivityTimeMath.ToUtcAssumingLocal(start);
            var endUtc = ActivityTimeMath.ToUtcAssumingLocal(end);
            var nowUtc = ActivityTimeMath.ToUtcAssumingLocal(utcNow);

            if (endUtc <= startUtc)
                return TimeSpan.Zero;

            var total = TimeSpan.Zero;

            foreach (var period in activity ?? Enumerable.Empty<ActivityModel>())
            {
                var periodStartUtc = ActivityTimeMath.ToUtcAssumingLocal(period.StartDate);
                var periodEndUtc = period.EndDate.HasValue
                    ? ActivityTimeMath.ToUtcAssumingLocal(period.EndDate.Value)
                    : Min(endUtc, nowUtc);

                var overlapStart = Max(periodStartUtc, startUtc);
                var overlapEnd = Min(periodEndUtc, endUtc);

                if (overlapEnd > overlapStart)
                    total += overlapEnd - overlapStart;
            }

            return total;
        }

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    }
}
