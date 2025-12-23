using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class BudgetProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2) return 0d;

            var cardObj = values[0];
            var nowObj = values[2];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return 0d;
            if (nowObj is null || nowObj == BindableProperty.UnsetValue) return 0d;

            if (cardObj is not BudgetCardModel b) return 0d;
            if (nowObj is not DateTime now) return 0d;

            var dailyTotal = b.GetDailyTopUpTotal(now.Date);
            if (dailyTotal <= 0) return 0d;

            var balance = b.GetBalance(now);
            var pctRemaining = balance / dailyTotal;

            // ProgressBar should show "used", like your orange fill showing spent.
            var pctUsed = 1.0 - pctRemaining;
            return Math.Clamp(pctUsed, 0, 1);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
