using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Notifications
{
    public sealed class SqliteNotificationLogService : INotificationLogService
    {
        private static readonly TimeSpan NotificationSentMatchWindow = TimeSpan.FromMinutes(5);

        private readonly ISqliteConnectionContext _context;
        private readonly ITimeZoneService _timeZoneService;

        public SqliteNotificationLogService(ISqliteConnectionContext context, ITimeZoneService? timeZoneService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _timeZoneService = timeZoneService ?? new TimeZoneService();
        }

        public async Task<IReadOnlyList<NotificationLogModel>> GetNotificationLogsAsync(int limit = 250)
        {
            await _context.InitializeAsync();

            var take = Math.Clamp(limit, 1, 1000);
            var rows = await _context.Db.QueryAsync<NotificationLogRow>(
                @"SELECT
                      NotificationLogId AS NotificationLogId,
                      ScheduleId        AS ScheduleId,
                      CardId            AS CardId,
                      CardTitle         AS CardTitle,
                      Note              AS Note,
                      Status            AS Status,
                      CreatedAt         AS CreatedAt,
                      ScheduledAt       AS ScheduledAt,
                      ScheduleFor       AS ScheduleFor,
                      SentAt            AS SentAt,
                      UpdatedAt         AS UpdatedAt,
                      Error             AS Error
                  FROM NotificationLog
                  ORDER BY ScheduleFor DESC, NotificationLogId DESC;");

            return rows
                .Select(ToNotificationLogModel)
                .OrderByDescending(log => log.ScheduleFor)
                .ThenByDescending(log => log.NotificationLogId)
                .Take(take)
                .ToList();
        }

        public async Task<NotificationLogModel> UpsertNotificationLogCreatedAsync(
            CardSchedule schedule,
            string? cardTitle,
            DateTime scheduleFor,
            DateTime createdAt)
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            await _context.InitializeAsync();

            var scheduleForUtc = ToUtcInstantForWrite(scheduleFor);
            var scheduleForIso = StrictTimeSerializer.SerializeUtcInstant(scheduleForUtc);
            var existing = (await _context.Db.QueryAsync<NotificationLogRow>(
                @"SELECT *
                  FROM NotificationLog
                  WHERE ScheduleId = ?
                  ORDER BY ScheduleFor DESC, NotificationLogId DESC;",
                schedule.ScheduleId))
                .FirstOrDefault(row => ParseInstantUtc(row.ScheduleFor) == scheduleForUtc);

            if (existing == null)
            {
                await _context.Db.ExecuteAsync(
                    @"INSERT INTO NotificationLog
                      (ScheduleId, CardId, CardTitle, Note, Status, CreatedAt, ScheduleFor, UpdatedAt)
                      VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
                    schedule.ScheduleId,
                    schedule.CardId,
                    cardTitle ?? "",
                    schedule.Note ?? "",
                    NotificationLogStatuses.Created,
                    SerializeInstantForDb(createdAt),
                    scheduleForIso,
                    SerializeInstantForDb(createdAt));

                var id = await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                return await GetNotificationLogByIdAsync(id)
                    ?? throw new InvalidOperationException("Notification log row was inserted but could not be read.");
            }

            await _context.Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET CardId = ?,
                      CardTitle = ?,
                      Note = ?,
                      UpdatedAt = ?
                  WHERE NotificationLogId = ?;",
                schedule.CardId,
                cardTitle ?? "",
                schedule.Note ?? "",
                SerializeInstantForDb(createdAt),
                existing.NotificationLogId);

            return await GetNotificationLogByIdAsync(existing.NotificationLogId)
                ?? throw new InvalidOperationException("Notification log row could not be read.");
        }

        public async Task MarkNotificationLogScheduledAsync(long notificationLogId, DateTime scheduledAt)
        {
            await _context.InitializeAsync();

            await _context.Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET Status = ?,
                      ScheduledAt = ?,
                      UpdatedAt = ?,
                      Error = NULL
                  WHERE NotificationLogId = ?
                    AND Status <> ?;",
                NotificationLogStatuses.Scheduled,
                SerializeInstantForDb(scheduledAt),
                SerializeInstantForDb(scheduledAt),
                notificationLogId,
                NotificationLogStatuses.Sent);
        }

        public async Task MarkNotificationLogScheduleErrorAsync(long notificationLogId, string error, DateTime updatedAt)
        {
            await _context.InitializeAsync();

            await _context.Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET Error = ?,
                      UpdatedAt = ?
                  WHERE NotificationLogId = ?;",
                error,
                SerializeInstantForDb(updatedAt),
                notificationLogId);
        }

        public async Task MarkNotificationLogSentAsync(
            CardSchedule schedule,
            string? cardTitle,
            DateTime firedAt,
            DateTime sentAt)
        {
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            await _context.InitializeAsync();

            var matchCutoffUtc = ToUtcInstantForWrite(firedAt.Add(NotificationSentMatchWindow));
            var candidates = await _context.Db.QueryAsync<NotificationLogRow>(
                @"SELECT *
                  FROM NotificationLog
                  WHERE ScheduleId = ?
                    AND Status IN (?, ?, ?)
                  ORDER BY ScheduleFor DESC, NotificationLogId DESC;",
                schedule.ScheduleId,
                NotificationLogStatuses.Created,
                NotificationLogStatuses.Scheduled,
                NotificationLogStatuses.Missed);

            var existing = candidates
                .Select(row => new { Row = row, ScheduleForUtc = ParseInstantUtc(row.ScheduleFor) })
                .Where(x => x.ScheduleForUtc <= matchCutoffUtc)
                .OrderByDescending(x => x.ScheduleForUtc)
                .ThenByDescending(x => x.Row.NotificationLogId)
                .Select(x => x.Row)
                .FirstOrDefault();

            var logId = existing?.NotificationLogId;
            if (logId == null)
            {
                var created = await UpsertNotificationLogCreatedAsync(schedule, cardTitle, firedAt, sentAt);
                logId = created.NotificationLogId;
            }

            await _context.Db.ExecuteAsync(
                @"UPDATE NotificationLog
                  SET CardId = ?,
                      CardTitle = ?,
                      Note = ?,
                      Status = ?,
                      SentAt = ?,
                      UpdatedAt = ?,
                      Error = NULL
                  WHERE NotificationLogId = ?;",
                schedule.CardId,
                cardTitle ?? "",
                schedule.Note ?? "",
                NotificationLogStatuses.Sent,
                SerializeInstantForDb(sentAt),
                SerializeInstantForDb(sentAt),
                logId.Value);
        }

        public async Task MarkOverdueNotificationLogsMissedAsync(DateTime now, TimeSpan gracePeriod)
        {
            await _context.InitializeAsync();

            var cutoffUtc = ToUtcInstantForWrite(now.Subtract(gracePeriod));
            var updatedAtIso = SerializeInstantForDb(now);
            var candidates = await _context.Db.QueryAsync<NotificationLogRow>(
                @"SELECT *
                  FROM NotificationLog
                  WHERE SentAt IS NULL
                    AND Status IN (?, ?);",
                NotificationLogStatuses.Created,
                NotificationLogStatuses.Scheduled);

            foreach (var row in candidates.Where(row => ParseInstantUtc(row.ScheduleFor) < cutoffUtc))
            {
                await _context.Db.ExecuteAsync(
                    @"UPDATE NotificationLog
                      SET Status = ?,
                          UpdatedAt = ?
                      WHERE NotificationLogId = ?;",
                    NotificationLogStatuses.Missed,
                    updatedAtIso,
                    row.NotificationLogId);
            }
        }

        private async Task<NotificationLogModel?> GetNotificationLogByIdAsync(long notificationLogId)
        {
            var row = (await _context.Db.QueryAsync<NotificationLogRow>(
                @"SELECT *
                  FROM NotificationLog
                  WHERE NotificationLogId = ?
                  LIMIT 1;",
                notificationLogId)).FirstOrDefault();

            return row == null ? null : ToNotificationLogModel(row);
        }

        private NotificationLogModel ToNotificationLogModel(NotificationLogRow row)
        {
            return new NotificationLogModel
            {
                NotificationLogId = row.NotificationLogId,
                ScheduleId = row.ScheduleId,
                CardId = row.CardId,
                CardTitle = row.CardTitle ?? "",
                Note = row.Note ?? "",
                Status = row.Status ?? NotificationLogStatuses.Created,
                CreatedAt = ParseInstantUtc(row.CreatedAt),
                ScheduledAt = string.IsNullOrWhiteSpace(row.ScheduledAt) ? null : ParseInstantUtc(row.ScheduledAt!),
                ScheduleFor = ParseInstantUtc(row.ScheduleFor),
                SentAt = string.IsNullOrWhiteSpace(row.SentAt) ? null : ParseInstantUtc(row.SentAt!),
                UpdatedAt = ParseInstantUtc(row.UpdatedAt),
                Error = row.Error
            };
        }

        private DateTime ParseInstantUtc(string value)
        {
            return LegacyTimeReader.ReadInstantUtc(value, _timeZoneService).UtcInstant;
        }

        private DateTime ToUtcInstantForWrite(DateTime value)
        {
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
                return new DateTime(value.Ticks, DateTimeKind.Utc);

            return value.Kind == DateTimeKind.Utc
                ? StrictTimeSerializer.RequireUtcInstant(value, nameof(value))
                : _timeZoneService.ToUtcFromLocal(value);
        }

        private string SerializeInstantForDb(DateTime value)
        {
            return StrictTimeSerializer.SerializeUtcInstant(ToUtcInstantForWrite(value));
        }

        private sealed class NotificationLogRow
        {
            public long NotificationLogId { get; set; }
            public long ScheduleId { get; set; }
            public long CardId { get; set; }
            public string CardTitle { get; set; } = "";
            public string Note { get; set; } = "";
            public string Status { get; set; } = "";
            public string CreatedAt { get; set; } = "";
            public string? ScheduledAt { get; set; }
            public string ScheduleFor { get; set; } = "";
            public string? SentAt { get; set; }
            public string UpdatedAt { get; set; } = "";
            public string? Error { get; set; }
        }
    }
}
