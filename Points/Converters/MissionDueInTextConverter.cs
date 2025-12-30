using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    internal class MissionDueInTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2) return "--";

            var cardObj = values[0];
            var nowObj = values[1];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return "--";
            if (nowObj is null || nowObj == BindableProperty.UnsetValue) return "--";

            if (cardObj is not MissionCardModel mission) return "--";
            if (nowObj is not DateTime now) return "--";

            var timeLeft = (mission.DueDate - DateTime.Now);

            var totalHours = (int)timeLeft.TotalHours;
            var formatted = $"{totalHours}:{timeLeft.Minutes:D2}:{timeLeft.Seconds:D2}";

            return formatted;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
