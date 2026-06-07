using Points.Services.Time;

namespace Points.Models
{
    internal static class ActivityTimeMath
    {
        private static readonly IClock Clock = new SystemClock();

        public static DateTime ToUtcAssumingLocal(DateTime value)
        {
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
            };
        }

        public static DateTime? ToUtcAssumingLocal(DateTime? value)
        {
            return value.HasValue ? ToUtcAssumingLocal(value.Value) : null;
        }

        public static DateTime UtcNow => Clock.UtcNow;

        public static DateTime LocalNow => Clock.LocalNow;
    }
}
