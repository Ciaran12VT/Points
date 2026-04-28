using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Global
{
    public static class GlobalVariables
    {
        private static DateTime _rangeStart = AsLocalWallClock(DateTime.Today);
        private static DateTime _rangeEnd = AsLocalWallClock(DateTime.Today.AddDays(1).AddTicks(-1));

        public static DateTime RangeStart
        {
            get => _rangeStart;
            set => _rangeStart = AsLocalWallClock(value);
        }

        public static DateTime RangeEnd
        {
            get => _rangeEnd;
            set => _rangeEnd = AsLocalWallClock(value);
        }

        private static DateTime AsLocalWallClock(DateTime value)
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
