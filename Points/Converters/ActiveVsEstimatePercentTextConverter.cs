using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Points.Global;
using Points.Models;

namespace Points.Converters
{
    // values expected (by index):
    // [0] = ActiveTime (TimeSpan or nullable)
    // [1] = EstCompletionTime (TimeSpan or nullable)
    // [2] = Now (DateTime) OPTIONAL: include this as a dummy binding to force re-evaluation every second
    public sealed class ActiveVsEstimatePercentTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            MissionCardModel model = (MissionCardModel)values[0];

            if (model == null) return "(—)";

            var range = GlobalVariables.GetCurrentRange();
            var active = model.GetActiveTime(range.Start, range.End); //GetTimeSpan(values, 0, culture);
            var est = model.EstCompletionTime; //GetTimeSpan(values, 1, culture);

            if (est <= TimeSpan.Zero) return "(—)";

            // Clamp active at >= 0
            if (active < TimeSpan.Zero) active = TimeSpan.Zero;

            var totalActiveSeconds = active.TotalSeconds;
            var totalEstSeconds = (est.HasValue ? est.Value : TimeSpan.Zero).TotalSeconds;

            if (totalActiveSeconds == 0 || totalEstSeconds == 0) return "(—)";

            double pct = (totalActiveSeconds / totalEstSeconds) * 100.0;
            return $"({pct:0.0}%)";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static TimeSpan GetTimeSpan(object[] values, int index, CultureInfo culture)
        {
            if (values == null || values.Length <= index || values[index] is null)
                return TimeSpan.Zero;

            var v = values[index];

            if (v is TimeSpan ts) return ts;

            // Safety: sometimes bindings end up as strings
            if (v is string s && TimeSpan.TryParse(s, culture, out var parsed))
                return parsed;

            return TimeSpan.Zero;
        }
    }
}
