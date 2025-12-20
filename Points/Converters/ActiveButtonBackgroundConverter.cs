using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class ActiveButtonBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            // expected: [0] = IsActive (bool), [1] = ValuePerMinute (double)
            if (values.Length < 2) return Colors.Gray;

            var isActive = values[0] is bool b && b;
            var vpm = values[1] is double d ? d : 0.0;

            if (!isActive)
                return Colors.Gray;

            return vpm < 0 ? Colors.Red : Colors.Green;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
