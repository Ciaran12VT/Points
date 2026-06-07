using Points.Global;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Sqlite;
using Points.Services.Time;

namespace Points.Services.HardMode
{
    public sealed class SqliteHardModePenaltyService : IHardModePenaltyService
    {
        private const double Epsilon = 0.0000001d;

        private readonly ISqliteConnectionContext _context;
        private readonly IActivityService _activity;
        private readonly ITimeZoneService _timeZoneService;

        public SqliteHardModePenaltyService(
            ISqliteConnectionContext context,
            IActivityService activity,
            ITimeZoneService timeZoneService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        }

        public async Task ReconcileAsync(DateTime utcNow)
        {
            var activeActivity = await _activity.GetCurrentActiveActivityAsync();

            await ReconcileAsync(
                SettingsProvider.HardModeEnabled,
                SettingsProvider.HardModeDamagePerMinuteValue,
                activeActivity != null,
                utcNow);
        }

        public async Task ReconcileAsync(
            bool hardModeEnabled,
            double penaltyPerMinute,
            bool hasActiveActivity,
            DateTime utcNow)
        {
            utcNow = StrictTimeSerializer.RequireUtcInstant(utcNow, nameof(utcNow));
            var normalizedPenalty = NormalizePenaltyPerMinute(penaltyPerMinute);

            if (!hardModeEnabled || hasActiveActivity || Math.Abs(normalizedPenalty) < Epsilon)
            {
                await CloseOpenIntervalAsync(utcNow);
                return;
            }

            await OpenOrUpdateIntervalAsync(utcNow, normalizedPenalty);
        }

        public async Task<double> GetValueAsync(DateTime rangeStart, DateTime rangeEnd, DateTime utcNow)
        {
            await _context.InitializeAsync();

            utcNow = StrictTimeSerializer.RequireUtcInstant(utcNow, nameof(utcNow));
            var rangeStartUtc = ToUtcInstant(rangeStart);
            var rangeEndUtc = ToUtcInstant(rangeEnd);

            if (rangeEndUtc <= rangeStartUtc)
                return 0d;

            var rows = await _context.Db.QueryAsync<HardModePenaltyIntervalRow>(
                @"
                SELECT HardModePenaltyIntervalID, Start, ""End"", ValuePerMinute
                FROM HardModePenaltyInterval
                WHERE Start < ?
                  AND (""End"" IS NULL OR ""End"" > ?)
                ORDER BY Start;",
                StrictTimeSerializer.SerializeUtcInstant(rangeEndUtc),
                StrictTimeSerializer.SerializeUtcInstant(rangeStartUtc));

            return rows
                .Select(ToModel)
                .Sum(interval => interval.GetValue(rangeStartUtc, rangeEndUtc, utcNow));
        }

        private async Task OpenOrUpdateIntervalAsync(DateTime utcNow, double penaltyPerMinute)
        {
            await _context.InitializeAsync();

            await _context.RunInTransactionAsync(tran =>
            {
                var open = tran.Query<HardModePenaltyIntervalRow>(
                    @"
                    SELECT HardModePenaltyIntervalID, Start, ""End"", ValuePerMinute
                    FROM HardModePenaltyInterval
                    WHERE ""End"" IS NULL
                    ORDER BY Start DESC
                    LIMIT 1;")
                    .FirstOrDefault();

                if (open != null && Math.Abs(open.ValuePerMinute - penaltyPerMinute) < Epsilon)
                    return;

                var nowIso = StrictTimeSerializer.SerializeUtcInstant(utcNow);

                if (open != null)
                {
                    tran.Execute(
                        @"UPDATE HardModePenaltyInterval
                          SET ""End"" = ?
                          WHERE HardModePenaltyIntervalID = ?;",
                        nowIso,
                        open.HardModePenaltyIntervalID);
                }

                tran.Execute(
                    @"INSERT INTO HardModePenaltyInterval (Start, ""End"", ValuePerMinute)
                      VALUES (?, NULL, ?);",
                    nowIso,
                    penaltyPerMinute);
            });
        }

        private async Task CloseOpenIntervalAsync(DateTime utcNow)
        {
            await _context.InitializeAsync();

            var nowIso = StrictTimeSerializer.SerializeUtcInstant(utcNow);

            await _context.Db.ExecuteAsync(
                @"UPDATE HardModePenaltyInterval
                  SET ""End"" = ?
                  WHERE ""End"" IS NULL;",
                nowIso);
        }

        private HardModePenaltyIntervalModel ToModel(HardModePenaltyIntervalRow row)
        {
            return new HardModePenaltyIntervalModel
            {
                Id = row.HardModePenaltyIntervalID,
                StartUtc = ParseInstantUtc(row.Start),
                EndUtc = string.IsNullOrWhiteSpace(row.End) ? null : ParseInstantUtc(row.End),
                ValuePerMinute = row.ValuePerMinute
            };
        }

        private DateTime ParseInstantUtc(string value)
        {
            return LegacyTimeReader.ReadInstantUtc(value, _timeZoneService).UtcInstant;
        }

        private DateTime ToUtcInstant(DateTime value)
        {
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
                return new DateTime(value.Ticks, DateTimeKind.Utc);

            return value.Kind == DateTimeKind.Utc
                ? StrictTimeSerializer.RequireUtcInstant(value, nameof(value))
                : _timeZoneService.ToUtcFromLocal(value);
        }

        private static double NormalizePenaltyPerMinute(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0d;

            return -Math.Abs(value);
        }

        private sealed class HardModePenaltyIntervalRow
        {
            public int HardModePenaltyIntervalID { get; set; }
            public string Start { get; set; } = string.Empty;
            public string? End { get; set; }
            public double ValuePerMinute { get; set; }
        }
    }
}
