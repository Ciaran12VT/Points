using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Scheduling;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Schedules
{
    public sealed class SqliteCardScheduleService : ICardScheduleService
    {
        private readonly ISqliteConnectionContext _context;

        public SqliteCardScheduleService(ISqliteConnectionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<CardSchedule>> GetCardSchedulesForCardAsync(long cardId)
        {
            await _context.InitializeAsync();

            var rows = await _context.Db.QueryAsync<CardScheduleRow>(
                @"SELECT
                      ScheduleID     AS ScheduleID,
                      CardID         AS CardID,
                      FrequencyType  AS FrequencyType,
                      FrequencyValue AS FrequencyValue,
                      FromDateTime   AS FromDateTime,
                      ToDateTime     AS ToDateTime,
                      IsEnabled      AS IsEnabled,
                      Note           AS Note
                  FROM CardSchedule
                  WHERE CardID = ?
                  ORDER BY FromDateTime;",
                cardId);

            return rows.Select(row => ToDomain(row, strictFrequencyType: false)).ToList();
        }

        public async Task<List<CardSchedule>> GetEnabledCardSchedulesAsync()
        {
            await _context.InitializeAsync();

            var rows = await _context.Db.QueryAsync<CardScheduleRow>(
                @"SELECT
                      ScheduleID     AS ScheduleID,
                      CardID         AS CardID,
                      FrequencyType  AS FrequencyType,
                      FrequencyValue AS FrequencyValue,
                      FromDateTime   AS FromDateTime,
                      ToDateTime     AS ToDateTime,
                      IsEnabled      AS IsEnabled,
                      Note           AS Note
                  FROM CardSchedule
                  WHERE IsEnabled = 1
                  ORDER BY FromDateTime;");

            return rows.Select(row => ToDomain(row, strictFrequencyType: true)).ToList();
        }

        public async Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId)
        {
            await _context.InitializeAsync();

            var rows = await _context.Db.QueryAsync<CardScheduleRow>(
                @"SELECT
                      ScheduleID     AS ScheduleID,
                      CardID         AS CardID,
                      FrequencyType  AS FrequencyType,
                      FrequencyValue AS FrequencyValue,
                      FromDateTime   AS FromDateTime,
                      ToDateTime     AS ToDateTime,
                      IsEnabled      AS IsEnabled,
                      Note           AS Note
                  FROM CardSchedule
                  WHERE ScheduleID = ?;",
                scheduleId);

            var row = rows.FirstOrDefault();
            return row == null ? null : ToDomain(row, strictFrequencyType: true);
        }

        public async Task SaveCardSchedulesAsync(long cardId, IEnumerable<CardSchedule> schedules)
        {
            await _context.InitializeAsync();

            var existing = await _context.Db.QueryAsync<CardScheduleRow>(
                @"SELECT
                      ScheduleID     AS ScheduleID,
                      CardID         AS CardID,
                      FrequencyType  AS FrequencyType,
                      FrequencyValue AS FrequencyValue,
                      FromDateTime   AS FromDateTime,
                      ToDateTime     AS ToDateTime,
                      IsEnabled      AS IsEnabled,
                      Note           AS Note
                  FROM CardSchedule
                  WHERE CardID = ?;",
                cardId);

            var remaining = existing.ToList();

            foreach (var schedule in schedules ?? Enumerable.Empty<CardSchedule>())
            {
                schedule.CardId = cardId;

                var fromDateTime = StrictTimeSerializer.SerializeLocalDateTime(
                    WallClockScheduleTime.NormalizeLocal(schedule.FromDateTime));
                var toDateTime = StrictTimeSerializer.SerializeNullableLocalDateTime(
                    WallClockScheduleTime.NormalizeLocal(schedule.ToDateTime));

                if (schedule.ScheduleId == 0)
                {
                    await _context.Db.ExecuteAsync(
                        @"INSERT INTO CardSchedule
                          (CardID, FrequencyType, FrequencyValue, FromDateTime, ToDateTime, IsEnabled, Note)
                          VALUES (?, ?, ?, ?, ?, ?, ?);",
                        cardId,
                        schedule.FrequencyType.ToString(),
                        schedule.FrequencyValue,
                        fromDateTime,
                        toDateTime,
                        schedule.IsEnabled ? 1 : 0,
                        schedule.Note ?? "");

                    schedule.ScheduleId = await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    await _context.Db.ExecuteAsync(
                        @"UPDATE CardSchedule
                          SET FrequencyType = ?,
                              FrequencyValue = ?,
                              FromDateTime = ?,
                              ToDateTime = ?,
                              IsEnabled = ?,
                              Note = ?
                          WHERE ScheduleID = ? AND CardID = ?;",
                        schedule.FrequencyType.ToString(),
                        schedule.FrequencyValue,
                        fromDateTime,
                        toDateTime,
                        schedule.IsEnabled ? 1 : 0,
                        schedule.Note ?? "",
                        schedule.ScheduleId,
                        cardId);

                    var keep = remaining.FirstOrDefault(row => row.ScheduleID == schedule.ScheduleId);
                    if (keep != null)
                        remaining.Remove(keep);
                }
            }

            foreach (var deleted in remaining)
            {
                await _context.Db.ExecuteAsync(
                    @"DELETE FROM CardSchedule WHERE ScheduleID = ? AND CardID = ?;",
                    deleted.ScheduleID,
                    cardId);
            }
        }

        private static CardSchedule ToDomain(CardScheduleRow row, bool strictFrequencyType)
        {
            var frequencyType = strictFrequencyType
                ? (FrequencyType)Enum.Parse(typeof(FrequencyType), row.FrequencyType)
                : ParseFrequencyTypeOrDefault(row.FrequencyType);

            return new CardSchedule
            {
                ScheduleId = row.ScheduleID,
                CardId = row.CardID,
                IsEnabled = row.IsEnabled != 0,
                Note = row.Note ?? "",
                FrequencyType = frequencyType,
                FrequencyValue = row.FrequencyValue,
                FromDateTime = LegacyTimeReader.ReadLocalDateTime(row.FromDateTime).LocalDateTime,
                ToDateTime = string.IsNullOrWhiteSpace(row.ToDateTime)
                    ? null
                    : LegacyTimeReader.ReadLocalDateTime(row.ToDateTime!).LocalDateTime
            };
        }

        private static FrequencyType ParseFrequencyTypeOrDefault(string? value)
        {
            return Enum.TryParse<FrequencyType>(value ?? "", out var frequencyType)
                ? frequencyType
                : default;
        }

        private sealed class CardScheduleRow
        {
            public long ScheduleID { get; set; }
            public long CardID { get; set; }
            public string FrequencyType { get; set; } = "";
            public int FrequencyValue { get; set; }
            public string FromDateTime { get; set; } = "";
            public string? ToDateTime { get; set; }
            public int IsEnabled { get; set; }
            public string Note { get; set; } = "";
        }
    }
}
