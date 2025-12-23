using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class ScStepCountTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4) return "0";

            if (values[0] is not ScStepModel step) return "0";
            if (values[1] is not DateTime start) return "0";
            if (values[2] is not DateTime end) return "0";

            // values[3] is RepsVersion (unused directly) — just here to trigger re-evaluation.

            string count = step.Count(start, end).ToString(CultureInfo.InvariantCulture);
            return count;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
