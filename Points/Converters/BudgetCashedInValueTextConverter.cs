using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class BudgetCashedInValueTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 3) return "Value: --";

            var cardObj = values[0];
            var startObj = values[1];
            var endObj = values[2];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return "Value: --";
            if (startObj is null || startObj == BindableProperty.UnsetValue) return "Value: --";
            if (endObj is null || endObj == BindableProperty.UnsetValue) return "Value: --";

            if (cardObj is not BudgetCardModel b) return "Value: --";
            if (startObj is not DateTime start) return "Value: --";
            if (endObj is not DateTime end) return "Value: --";

            var v = b.GetCashedInValue(start, end);
            return $"Value: {v:0}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
