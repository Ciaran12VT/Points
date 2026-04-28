namespace Points.Models
{
    public enum TimeScope
    {
        Daily,
        Weekly,
        Monthly
    }

    public class TimeScopeRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public TimeScopeRange(TimeScope timeScope, DateTime now)
        {
            var localNow = NormalizeLocal(now);

            switch (timeScope)
            {
                case TimeScope.Daily:
                    Start = localNow.Date;
                    End = Start.AddDays(1).AddTicks(-1);
                    break;

                case TimeScope.Weekly:
                    var daysSinceMonday = (7 + (localNow.DayOfWeek - DayOfWeek.Monday)) % 7;
                    Start = localNow.Date.AddDays(-daysSinceMonday);
                    End = Start.AddDays(7).AddTicks(-1);
                    break;

                case TimeScope.Monthly:
                    Start = new DateTime(localNow.Year, localNow.Month, 1);
                    End = Start.AddMonths(1).AddTicks(-1);
                    break;
            }

            Start = NormalizeLocal(Start);
            End = NormalizeLocal(End);
        }

        public double GetPercentageComplete(DateTime atTime)
        {
            var localAtTime = NormalizeLocal(atTime);

            if (localAtTime > End) return 100;
            if (localAtTime < Start) return 0;

            var total = (End - Start).TotalMilliseconds;
            if (total <= 0) return 100d;

            var elapsed = (localAtTime - Start).TotalMilliseconds;
            var pct = elapsed / total * 100d;

            if (pct < 0d) return 0d;
            if (pct > 100d) return 100d;
            return pct;
        }

        private static DateTime NormalizeLocal(DateTime value)
        {
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
                return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

            var local = value.Kind == DateTimeKind.Utc
                ? value.ToLocalTime()
                : value;

            return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        }
    }
}
