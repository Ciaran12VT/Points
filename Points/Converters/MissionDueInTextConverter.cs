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
                else if(totalHours <= 0 && totalHours > -23)
                {
                    int mins = timeLeft.Minutes < 0 ? timeLeft.Minutes * -1 : timeLeft.Minutes;
                    int secs = timeLeft.Seconds < 0 ? timeLeft.Seconds * -1 : timeLeft.Seconds;
                    result = $"{labelText}: {totalHours}:{mins:D2}:{secs:D2}";
                }
                else
                {
                    var text = GetTextVersion(timeLeft, true);
                    result = $"{labelText}: {text}";
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
                    int mins = timeUsed.Minutes < 0 ? timeUsed.Minutes * -1 : timeUsed.Minutes;
                    int secs = timeUsed.Seconds < 0 ? timeUsed.Seconds * -1 : timeUsed.Seconds;
                    result = $"Took: {totalHours}:{mins:D2}:{secs:D2}";
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
                    int mins = timeToBeUsed.Minutes < 0 ? timeToBeUsed.Minutes * -1 : timeToBeUsed.Minutes;
                    int secs = timeToBeUsed.Seconds < 0 ? timeToBeUsed.Seconds * -1 : timeToBeUsed.Seconds;
                    result = $"Available For: {totalHours}:{mins:D2}:{secs:D2}";
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
            else if ((int)time.Days == -1)
            {
                text += $"{time.Days * -1} day ";
            }
            else if ((int)time.Days < -1)
            {
                text += $"{time.Days * -1} days ";
            }

            if ((int)time.Hours >= 1)
            {
                text += $"{hrs} hours";
            }
            else if ((int)time.Hours == 1)
            {
                text += $"{hrs} hour";
            }
            else if ((int)time.Hours == -1)
            {
                text += $"{time.Hours * -1} hour";
            }
            else if ((int)time.Hours < -1)
            {
                text += $"{time.Hours * -1} hours";
            }

            return text;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
