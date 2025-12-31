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

            string result = "";

            if(mission.IsAvailable)
            {
                var timeLeft = (mission.DueDate - DateTime.Now);
                var totalHours = (int)timeLeft.TotalHours;
                result = $"Due In: {totalHours}:{timeLeft.Minutes:D2}:{timeLeft.Seconds:D2}";
            }
            else if(mission.IsComplete)
            {
                var timeUsed = mission.GetActiveTime(mission.AvailableFromDate, mission.CompletedDate ?? DateTime.Now);
                var totalHours = (int)timeUsed.TotalHours;
                result = $"Took: {totalHours}:{timeUsed.Minutes:D2}:{timeUsed.Seconds:D2}";
            }
            else
            {
                var timeToBeUsed = (mission.DueDate - mission.AvailableFromDate);
                var totalHours = (int)timeToBeUsed.TotalHours;
                result = $"Available For: {totalHours}:{timeToBeUsed.Minutes:D2}:{timeToBeUsed.Seconds:D2}";
            }

            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
