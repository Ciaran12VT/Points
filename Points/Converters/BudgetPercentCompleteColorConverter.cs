using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    internal class BudgetPercentCompleteColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 3) return Colors.Grey;

            var cardObj = values[0];
            var startObj = values[1];
            var endObj = values[2];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return Colors.Grey;
            if (startObj is null || startObj == BindableProperty.UnsetValue) return Colors.Grey;
            if (endObj is null || endObj == BindableProperty.UnsetValue) return Colors.Grey;

            if (cardObj is not BudgetCardModel b) return Colors.Grey;
            if (startObj is not DateTime start) return Colors.Grey;
            if (endObj is not DateTime end) return Colors.Grey;

            var dailyTotal = b.GetDailyTopUpTotal(DateTime.Now);
            if (dailyTotal <= 0) return "%: --";

            var balance = b.GetBalance(DateTime.Now);
            var pct = (balance / dailyTotal) * 100.0;

            var color = Colors.Red;
            if(pct > 0 && pct <= 25) color = Colors.Orange;
            if (pct > 25 && pct <= 75) color = Colors.Yellow;
            if (pct > 75 && pct <= 100) color = Colors.Green;
            if (pct > 100) color = Colors.Silver;

            return color;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
