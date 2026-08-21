using Points.Services.Time;

namespace Points.Global
{
    internal readonly record struct CurrentDayRangeSnapshot(
        DateTime Start,
        DateTime End,
        bool Changed,
        bool FollowsCurrentDay);

    /// <summary>
    /// Owns the process-wide display range and keeps the default "Today" range
    /// aligned with the current local calendar day.
    /// </summary>
    internal sealed class CurrentDayRangeState
    {
        private readonly object _sync = new();
        private DateTime _start;
        private DateTime _end;
        private bool _followsCurrentDay;

        public CurrentDayRangeState(DateTime localNow)
        {
            var today = TimeDisplayFormatter.ToLocalInstant(localNow).Date;
            _start = today;
            _end = EndOfDay(today);
            _followsCurrentDay = true;
        }

        public CurrentDayRangeSnapshot EnsureCurrentDay(DateTime localNow)
        {
            lock (_sync)
            {
                if (!_followsCurrentDay)
                    return Snapshot(changed: false);

                var today = TimeDisplayFormatter.ToLocalInstant(localNow).Date;
                var end = EndOfDay(today);
                var changed = _start != today || _end != end;

                if (changed)
                {
                    _start = today;
                    _end = end;
                }

                return Snapshot(changed);
            }
        }

        public CurrentDayRangeSnapshot SetRange(
            DateTime start,
            DateTime end,
            DateTime localNow,
            bool followsCurrentDay)
        {
            lock (_sync)
            {
                var today = TimeDisplayFormatter.ToLocalInstant(localNow).Date;
                _followsCurrentDay = followsCurrentDay;

                if (_followsCurrentDay)
                {
                    _start = today;
                    _end = EndOfDay(today);
                }
                else
                {
                    _start = TimeDisplayFormatter.ToLocalInstant(start);
                    _end = TimeDisplayFormatter.ToLocalInstant(end);
                }

                return Snapshot(changed: true);
            }
        }

        private CurrentDayRangeSnapshot Snapshot(bool changed)
        {
            return new CurrentDayRangeSnapshot(
                _start,
                _end,
                changed,
                _followsCurrentDay);
        }

        private static DateTime EndOfDay(DateTime date)
        {
            return date.AddDays(1).AddTicks(-1);
        }
    }
}
