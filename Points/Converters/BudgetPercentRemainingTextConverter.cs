using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class BudgetPercentRemainingTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 3) return "%: --";

            var cardObj = values[0];
            var nowObj = values[2];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return "%: --";
            if (nowObj is null || nowObj == BindableProperty.UnsetValue) return "%: --";

            if (cardObj is not BudgetCardModel b) return "%: --";
            if (nowObj is not DateTime now) return "%: --";

            var dailyTotal = b.GetDailyTopUpTotal(now.Date);
            if (dailyTotal <= 0) return "%: --";

            var balance = b.GetBalance(now);
            var pct = (balance / dailyTotal) * 100.0;

            return $"%: {pct:0}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
