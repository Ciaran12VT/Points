using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class ValueTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 3) return "Value: 0.00";
            if (values[0] is not ICardModel card) return "Value: --";
            if (values[1] is not DateTime start) return "Value: --";
            if (values[2] is not DateTime end) return "Value: --";

            var v = MultiplierValueCalculator.GetValue(card, start, end);
            return $"Value: {v:F2}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
