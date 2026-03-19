using Points.Models;

namespace Points.Services.Sqlite
{
    public sealed partial class ActivityRepository
    {
        private static class ActivityMapper
        {
            public static ActivityModel ToModel(ActivityRow row, Func<string, DateTime> parseIsoDateTime)
            {
                if (row == null) throw new ArgumentNullException(nameof(row));
                if (string.IsNullOrWhiteSpace(row.Start))
                    throw new InvalidOperationException("ActivityRow.Start is required.");

                DateTime? end = null;
                if (!string.IsNullOrWhiteSpace(row.End))
                    end = parseIsoDateTime(row.End);

                return new ActivityModel
                {
                    Id = row.ActivityID,
                    CardID = row.CardID,
                    StartDate = parseIsoDateTime(row.Start),
                    EndDate = end,
                    RateName = row.ValueRateName ?? string.Empty,
                    ValuePerMinute = row.ValuePerMinute
                };
            }
        }
    }
}