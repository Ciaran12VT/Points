using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class MissionCurrentValueColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2) return Colors.Green;

            var cardObj = values[0];
            var nowObj = values[1];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return Colors.Green;
            if (nowObj is null || nowObj == BindableProperty.UnsetValue) return Colors.Green;

            if (cardObj is not MissionCardModel mission) return Colors.Green;
            if (nowObj is not DateTime now) return Colors.Green;

            var v = mission.GetCurrentValue(now);
            return v < 0 ? Colors.Red : Colors.Green;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
