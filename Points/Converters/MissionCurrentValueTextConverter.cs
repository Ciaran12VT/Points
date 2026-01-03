using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class MissionCurrentValueTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                if (values is null || values.Length < 2)
                    return "Value: --";

                var cardObj = values[0];
                var nowObj = values[1];

                // MAUI can pass UnsetValue during initial binding
                if (cardObj is null || cardObj == BindableProperty.UnsetValue)
                    return "Value: --";

                if (nowObj is null || nowObj == BindableProperty.UnsetValue)
                    return "Value: --";

                if (cardObj is not MissionCardModel mission)
                    return "Value: --";

                if (nowObj is not DateTime now)
                    return "Value: --";

                // DEBUG
                if (cardObj is MissionCardModel mc)
                {
                    if (mc.Title == "Mission B")
                    {

                    }
                }

                var v = mission.IsPending ? mission.Value : mission.GetCurrentValue(now);
                var formatted = $"Value: {v:F2}";
                return formatted;
            }
            catch
            {
                // Never crash XAML creation because of a transient bind state
                return "Value: --";
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
