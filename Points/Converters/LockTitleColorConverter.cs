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
    public sealed class LockTitleColorConverter : IMultiValueConverter
    {
        // values[0] = original title color (string or Color)
        // values[1] = card (IActiveCardModel)
        // values[2] = now (DateTime)
        // values[3] = allCards (IEnumerable<IActiveCardModel>)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4)
                return Colors.White;

            var original = values[0];
            var card = values[1] as IActiveCardModel;
            var now = values[2] is DateTime dt ? dt : DateTime.Now;
            var allCards = values[3] as IEnumerable<IActiveCardModel>;

            if (card != null && allCards != null)
            {
                if (LockEvaluator.IsLockedNow(card, now, allCards.ToList(), out _))
                    return Colors.Gray;
            }

            // Preserve original color if not locked
            if (original is Color c)
                return c;

            if (original is string s && Color.TryParse(s, out var parsed))
                return parsed;

            return Colors.White;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
