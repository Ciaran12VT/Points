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

            if(mission.IsAvailable && !mission.IsComplete)
            {
                var timeLeft = (mission.DueDate - DateTime.Now);
                var totalHours = (int)timeLeft.TotalHours;

                var labelText = timeLeft > TimeSpan.Zero ? "Due In" : "Overdue By";

                if (mission.DueDate.Date == DateTime.Today.AddDays(1).Date)
                {
                    result = $"Due Tomorrow at " + mission.DueDate.ToString("hh") + (mission.DueDate.Hour >= 12 ? "am" : "pm");
                    return result;
                }

                if (totalHours > 23)
                {
                    var text = GetTextVersion(timeLeft, true);
                    result = $"{labelText}: {text}";
                }
                else
                {
                    result = $"{labelText}: {totalHours}:{timeLeft.Minutes:D2}:{timeLeft.Seconds:D2}";
                }
            }
            else if(mission.IsComplete)
            {
                var timeUsed = mission.GetActiveTime(mission.AvailableFromDate, mission.CompletedDate ?? DateTime.Now);
                var totalHours = (int)timeUsed.TotalHours;

                if (totalHours > 23)
                {
                    var text = GetTextVersion(timeUsed, true);
                    result = $"Took: {text}";
                }
                else
                {
                    result = $"Took: {totalHours}:{timeUsed.Minutes:D2}:{timeUsed.Seconds:D2}";
                }
            }
            else
            {
                var timeToBeUsed = (mission.DueDate - mission.AvailableFromDate);
                var totalHours = (int)timeToBeUsed.TotalHours;

                if (totalHours > 23)
                {
                    var text = GetTextVersion(timeToBeUsed, true);
                    result = $"Available For: {text}";
                }
                else
                {
                    result = $"Available For: {totalHours}:{timeToBeUsed.Minutes:D2}:{timeToBeUsed.Seconds:D2}";
                }
            }

            return result;
        }

        private string GetTextVersion(TimeSpan time, bool isForDue = false)
        {
            string days = ((int)time.Days).ToString();
            string hrs = ((int)time.Hours).ToString();
            string min = ((int)time.Minutes).ToString();
            string sec = ((int)time.Seconds).ToString();

            string text = "";

            if((int)time.Days > 1)
            {
                text += $"{days} days ";
            }
            else if ((int)time.Days == 1)
            {
                text += $"{days} day ";
            }

            if ((int)time.Hours >= 1)
            {
                text += $"{hrs} hours";
            }
            else if ((int)time.Hours == 1)
            {
                text += $"{hrs} hour";
            }

            return text;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
