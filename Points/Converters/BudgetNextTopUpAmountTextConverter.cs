using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class BudgetNextTopUpAmountTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2) return "Next Top-Up Value: --";

            var cardObj = values[0];
            var nowObj = values[2];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return "Next Top-Up Value: --";
            if (nowObj is null || nowObj == BindableProperty.UnsetValue) return "Next Top-Up Value: --";

            if (cardObj is not BudgetCardModel b) return "Next Top-Up Value: --";
            if (nowObj is not DateTime now) return "Next Top-Up Value: --";

            var next = b.GetNextTopUp(now);
            if (next is null) return "Next Top-Up Value: --";

            return $"Next Top-Up Value: {next.Value.Amount:0}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
