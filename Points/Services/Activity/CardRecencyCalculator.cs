using Points.Models;

namespace Points.Services.Activity
{
    public static class CardRecencyCalculator
    {
        public static DateTime Latest(DateTime baseline, IEnumerable<DateTime>? candidates)
        {
            var latest = ActivityTimeMath.ToUtcAssumingLocal(baseline);

            foreach (var candidate in candidates ?? Enumerable.Empty<DateTime>())
            {
                var candidateUtc = ActivityTimeMath.ToUtcAssumingLocal(candidate);
                if (candidateUtc > latest)
                    latest = candidateUtc;
            }

            return latest;
        }
    }
}
