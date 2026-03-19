using System.Globalization;
using Points.Models;

namespace Points.Services.Sqlite
{
    public sealed partial class LockRepository
    {
        private static class LockMapper
        {
            public static LockModel ToDomain(
                LockRow row,
                IEnumerable<LockScheduleRow> scheduleRows,
                IEnumerable<LockTaskDependencyRow> dependencyRows)
            {
                return new LockModel
                {
                    LockId = row.LockId,
                    LockNumber = row.LockNumber,
                    CardId = row.CardId,
                    TimeWindowStart = TimeOnly.ParseExact(
                        row.TimeWindowStart,
                        "HH:mm:ss",
                        CultureInfo.InvariantCulture),
                    TimeWindowEnd = TimeOnly.ParseExact(
                        row.TimeWindowEnd,
                        "HH:mm:ss",
                        CultureInfo.InvariantCulture),
                    Schedules = scheduleRows.Select(LockScheduleMapper.ToDomain).ToList(),
                    Dependencies = dependencyRows.Select(LockTaskDependencyMapper.ToDomain).ToList()
                };
            }
        }
    }
}