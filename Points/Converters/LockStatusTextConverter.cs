using Points.Models;
using Points.Services.Locks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Points.Converters
{
    public sealed class LockStatusTextConverter : IMultiValueConverter
    {
        // values[0] = card (IActiveCardModel)
        // values[1] = now (DateTime)
        // values[2] = allCards (IEnumerable<IActiveCardModel>)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3) return "Status: ";
            if (values[0] is not IActiveCardModel card) return "Status: ";
            if (values[1] is not DateTime now) return "Status: ";
            if (values[2] is not IEnumerable<IActiveCardModel> allCards) return "Status: ";

            if (LockEvaluator.IsLockedNow(card, now, allCards.ToList(), out var until))
            {
                var rem = LockEvaluator.FormatRemaining(now, until);
                return $"Locked: Available in {rem}";
            }

            // Fallback to existing status string if present
            // (Your models have Status; keeping your existing “dynamic” approach.)
            try
            {
                var status = (card as dynamic)?.Status as string;
                return string.IsNullOrWhiteSpace(status) ? "Status: " : $"Status: {status}";
            }
            catch
            {
                return "Status: ";
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}