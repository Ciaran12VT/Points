using Points.Global;
using Points.Models;
using System.Globalization;

namespace Points.Converters
{
    public sealed class ActiveVsEstimatePercentColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            MissionCardModel model = (MissionCardModel)values[0];

            if (model == null) return Colors.Grey;

            var active = model.GetActiveTime(GlobalVariables.RangeStart, GlobalVariables.RangeEnd); //GetTimeSpan(values, 0, culture);
            var est = model.EstCompletionTime; //GetTimeSpan(values, 1, culture);

            if (!est.HasValue || est.Value <= TimeSpan.Zero)
                return Colors.Gray;

            if (active < TimeSpan.Zero || !est.HasValue) active = TimeSpan.Zero;

            double pct = (active.TotalSeconds / (est.HasValue ? est.Value : TimeSpan.Zero).TotalSeconds) * 100.0;

            if (pct < 75.0) return Colors.Green;
            if (pct <= 100.0) return Colors.Orange;
            return Colors.Red;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();

        private static TimeSpan GetTimeSpan(object[] values, int index, CultureInfo culture)
        {
            if (values == null || values.Length <= index || values[index] is null)
                return TimeSpan.Zero;

            var v = values[index];

            if (v is TimeSpan ts) return ts;

            if (v is string s && TimeSpan.TryParse(s, culture, out var parsed))
                return parsed;

            return TimeSpan.Zero;
        }
    }
}
