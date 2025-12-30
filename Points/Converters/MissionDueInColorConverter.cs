using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    internal class MissionDueInColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2) return Colors.Grey;

            var cardObj = values[0];
            var nowObj = values[1];

            if (cardObj is null || cardObj == BindableProperty.UnsetValue) return Colors.Grey;
            if (nowObj is null || nowObj == BindableProperty.UnsetValue) return Colors.Grey;

            if (cardObj is not MissionCardModel mission) return Colors.Grey;
            if (nowObj is not DateTime now) return Colors.Grey;

            var estCompletionTime = mission.EstCompletionTime ?? TimeSpan.Zero;
            var timeLeft = (mission.DueDate - DateTime.Now);

            if(timeLeft == TimeSpan.Zero) return Colors.Red;

            if (estCompletionTime == TimeSpan.Zero)
            {
                estCompletionTime = mission.DueDate - mission.AvailableFromDate;
            }

            double percentLeft = ((double)timeLeft.Ticks / (double)estCompletionTime.Ticks) * 100;
           
            return percentLeft > 25 ? Colors.Yellow : (percentLeft > 0 ? Colors.DarkOrange : Colors.Red);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
