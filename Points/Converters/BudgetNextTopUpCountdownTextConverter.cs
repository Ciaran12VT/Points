using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class BudgetNextTopUpCountdownTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2) return "Next Top-Up In: --:--:--";

            var cardObj = values[0];
            var nowObj = values[1];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return "Next Top-Up In: --:--:--";
            if (nowObj is null || nowObj == BindableProperty.UnsetValue) return "Next Top-Up In: --:--:--";

            if (cardObj is not BudgetCardModel b) return "Next Top-Up In: --:--:--";
            if (nowObj is not DateTime now) return "Next Top-Up In: --:--:--";

            var next = b.GetNextTopUp(now);
            if (next is null) return "Next Top-Up In: --:--:--";

            var remaining = next.Value.When - now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            return $"Next Top-Up In: {remaining:hh\\:mm\\:ss}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
