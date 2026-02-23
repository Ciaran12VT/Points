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
    public sealed class IsLockedNowConverter : IMultiValueConverter
    {
        // values[0] = card (IActiveCardModel)
        // values[1] = now (DateTime)
        // values[2] = allCards (IEnumerable<IActiveCardModel>)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // If we can't evaluate, default to "not locked" to avoid disabling UI accidentally.
            if (values == null || values.Length < 3) return false;

            if (values[0] is not IActiveCardModel card) return false;
            if (values[1] is not DateTime now) return false;
            if (values[2] is not IEnumerable<IActiveCardModel> allCards) return false;

            return LockEvaluator.IsLockedNow(card, now, allCards.ToList(), out _);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
