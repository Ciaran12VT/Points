using Points.Models;
using Points.Services.Locks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Converters
{
    public sealed class NotLockedConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3) return true;
            if (values[0] is not IActiveCardModel card) return true;
            if (values[1] is not DateTime now) return true;
            if (values[2] is not IEnumerable<IActiveCardModel> allCards) return true;

            return !LockEvaluator.IsLockedNow(card, now, allCards.ToList(), out _);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
