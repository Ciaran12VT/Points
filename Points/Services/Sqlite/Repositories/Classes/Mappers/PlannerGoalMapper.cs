using System.Globalization;
using Points.Models;
using Points.ViewModels;

namespace Points.Services.Sqlite
{
    public sealed partial class PlannerRepository
    {
        private static class PlannerGoalMapper
        {
            public static PlannerGoalDetailsModel ToDomain(PlannerGoalRow row)
            {
                return new PlannerGoalDetailsModel
                {
                    CardId = row.CardID,
                    TimeScope = ParseTimeScope(row.TimeScope),
                    GoalHrs = row.GoalHrs,
                    Enabled = row.Enabled != 0,
                    DeFactoStart = ParseNullableTimeOnly(row.DeFactoStart),
                    DeFactoEnd = ParseNullableTimeOnly(row.DeFactoEnd)
                };
            }

            public static string? ToDbTimeOnly(TimeOnly? value)
            {
                return value?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }

            private static TimeScope ParseTimeScope(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    Enum.TryParse<TimeScope>(value, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }

                return TimeScope.Daily;
            }

            private static TimeOnly? ParseNullableTimeOnly(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return null;

                if (TimeOnly.TryParseExact(
                    value,
                    "HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
                {
                    return parsed;
                }

                return null;
            }
        }
    }
}