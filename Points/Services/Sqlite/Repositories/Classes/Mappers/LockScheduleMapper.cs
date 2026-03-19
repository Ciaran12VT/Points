using System.Globalization;
using Points.Models;

namespace Points.Services.Sqlite
{
    public sealed partial class LockRepository
    {
        private static class LockScheduleMapper
        {
            public static LockScheduleModel ToDomain(LockScheduleRow row)
            {
                return new LockScheduleModel
                {
                    ScheduleId = row.ScheduleId,
                    LockId = row.LockId,
                    FrequencyType = ParseFrequencyType(row.FrequencyType),
                    FrequencyValue = row.FrequencyValue,
                    FromDateTime = DateTime.Parse(
                        row.FromDateTime,
                        null,
                        DateTimeStyles.RoundtripKind),
                    ToDateTime = string.IsNullOrWhiteSpace(row.ToDateTime)
                        ? null
                        : DateTime.Parse(
                            row.ToDateTime!,
                            null,
                            DateTimeStyles.RoundtripKind)
                };
            }

            private static FrequencyType ParseFrequencyType(string value)
            {
                if (Enum.TryParse<FrequencyType>(value, ignoreCase: true, out var parsed))
                    return parsed;

                return (FrequencyType)0;
            }
        }
    }
}