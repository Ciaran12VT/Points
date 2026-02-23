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
    public sealed class LockedToOpacityConverter : IMultiValueConverter
    {
        // values[0] = card (IActiveCardModel)
        // values[1] = now (DateTime)
        // values[2] = allCards (IEnumerable<IActiveCardModel>)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
                return 1.0; // default full opacity

            if (values[0] is not IActiveCardModel card)
                return 1.0;

            if (values[1] is not DateTime now)
                return 1.0;

            if (values[2] is not IEnumerable<IActiveCardModel> allCards)
                return 1.0;

            var isLocked = LockEvaluator.IsLockedNow(card, now, allCards.ToList(), out _);

            // Locked = faded
            return isLocked ? 0.4 : 1.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
