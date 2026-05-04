using Points.Services.Time;

namespace Points.Models
{
    public sealed class HardModePenaltyIntervalModel
    {
        public int Id { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public double ValuePerMinute { get; set; }

        public double GetValue(DateTime rangeStartUtc, DateTime rangeEndUtc, DateTime utcNow)
        {
            rangeStartUtc = StrictTimeSerializer.RequireUtcInstant(rangeStartUtc, nameof(rangeStartUtc));
            rangeEndUtc = StrictTimeSerializer.RequireUtcInstant(rangeEndUtc, nameof(rangeEndUtc));
            utcNow = StrictTimeSerializer.RequireUtcInstant(utcNow, nameof(utcNow));
            var startUtc = StrictTimeSerializer.RequireUtcInstant(StartUtc, nameof(StartUtc));
            var endUtc = EndUtc.HasValue
                ? StrictTimeSerializer.RequireUtcInstant(EndUtc.Value, nameof(EndUtc))
                : utcNow;

            if (rangeEndUtc <= rangeStartUtc || endUtc <= startUtc)
                return 0d;

            var overlapStart = startUtc > rangeStartUtc ? startUtc : rangeStartUtc;
            var overlapEnd = endUtc < rangeEndUtc ? endUtc : rangeEndUtc;

            return overlapEnd > overlapStart
                ? ValuePerMinute * (overlapEnd - overlapStart).TotalMinutes
                : 0d;
        }
    }
}
