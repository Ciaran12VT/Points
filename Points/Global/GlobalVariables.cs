using System;
using Points.Services.Time;

namespace Points.Global
{
    public static class GlobalVariables
    {
        private static readonly IClock Clock = new SystemClock();
        private static readonly CurrentDayRangeState RangeState = new(Clock.LocalNow);

        public static DateTime RangeStart => GetCurrentRange().Start;

        public static DateTime RangeEnd => GetCurrentRange().End;

        internal static CurrentDayRangeSnapshot GetCurrentRange(DateTime localNow)
        {
            return RangeState.EnsureCurrentDay(localNow);
        }

        internal static CurrentDayRangeSnapshot SetRange(
            DateTime start,
            DateTime end,
            DateTime localNow,
            bool followsCurrentDay)
        {
            return RangeState.SetRange(start, end, localNow, followsCurrentDay);
        }

        internal static CurrentDayRangeSnapshot GetCurrentRange()
        {
            return GetCurrentRange(Clock.LocalNow);
        }
    }
}
