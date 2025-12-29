using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public class ActiveTimeTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 3) return "00:00:00";
            if (values[0] is not IActiveCardModel card) return " --:--:--";
            if (values[1] is not DateTime start) return "--:--:--";
            if (values[2] is not DateTime end) return "--:--:--";

            var ts = card.GetActiveTime(start, end);
            return $"{ts:hh\\:mm\\:ss}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
