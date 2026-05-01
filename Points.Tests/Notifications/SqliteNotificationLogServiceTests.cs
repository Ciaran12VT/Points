using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Notifications;
using Points.Services.Persistence;
using Points.Services.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Notifications
{
    public sealed class SqliteNotificationLogServiceTests
    {
        [Fact]
        public async Task UpsertNotificationLogCreatedAsync_InsertsAndUpdatesSameScheduleOccurrence()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteNotificationLogService(context, new TimeZoneService());
            var schedule = NewSchedule(scheduleId: 7, cardId: 101, note: "Original note");

            var first = await service.UpsertNotificationLogCreatedAsync(
                schedule,
                "Original title",
                Utc(12),
                Utc(9));

            schedule.CardId = 202;
            schedule.Note = "Updated note";

            var second = await service.UpsertNotificationLogCreatedAsync(
                schedule,
                "Updated title",
                Utc(12),
                Utc(10));

            Assert.Equal(first.NotificationLogId, second.NotificationLogId);
            Assert.Equal(202, second.CardId);
            Assert.Equal("Updated title", second.CardTitle);
            Assert.Equal("Updated note", second.Note);
            Assert.Equal(NotificationLogStatuses.Created, second.Status);
            Assert.Equal(Utc(10), second.UpdatedAt);

            Assert.Single(await service.GetNotificationLogsAsync());
        }

        [Fact]
        public async Task MarkNotificationLogScheduledAsync_SetsScheduledStatusAndClearsError()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteNotificationLogService(context, new TimeZoneService());
            var log = await service.UpsertNotificationLogCreatedAsync(
                NewSchedule(),
                "Focus",
                Utc(12),
                Utc(9));

            await service.MarkNotificationLogScheduleErrorAsync(log.NotificationLogId, "Could not schedule", Utc(9, 5));
            await service.MarkNotificationLogScheduledAsync(log.NotificationLogId, Utc(9, 10));

            var saved = Assert.Single(await service.GetNotificationLogsAsync());
            Assert.Equal(NotificationLogStatuses.Scheduled, saved.Status);
            Assert.Equal(Utc(9, 10), saved.ScheduledAt);
            Assert.Equal(Utc(9, 10), saved.UpdatedAt);
            Assert.Null(saved.Error);
        }

        [Fact]
        public async Task MarkNotificationLogScheduledAsync_DoesNotDowngradeSentLog()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteNotificationLogService(context, new TimeZoneService());
            var schedule = NewSchedule();
            var log = await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(12), Utc(9));

            await service.MarkNotificationLogSentAsync(schedule, "Focus", Utc(12), Utc(12, 1));
            await service.MarkNotificationLogScheduledAsync(log.NotificationLogId, Utc(12, 2));

            var saved = Assert.Single(await service.GetNotificationLogsAsync());
            Assert.Equal(NotificationLogStatuses.Sent, saved.Status);
            Assert.Equal(Utc(12, 1), saved.SentAt);
            Assert.Null(saved.ScheduledAt);
        }

        [Fact]
        public async Task MarkNotificationLogSentAsync_MatchesMostRecentEligibleOccurrence()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteNotificationLogService(context, new TimeZoneService());
            var schedule = NewSchedule();

            await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(11), Utc(8));
            await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(12), Utc(9));

            await service.MarkNotificationLogSentAsync(schedule, "Focus", Utc(12), Utc(12, 3));

            var logs = await service.GetNotificationLogsAsync();
            var current = Assert.Single(logs, x => x.ScheduleFor == Utc(12));
            var previous = Assert.Single(logs, x => x.ScheduleFor == Utc(11));

            Assert.Equal(NotificationLogStatuses.Sent, current.Status);
            Assert.Equal(Utc(12, 3), current.SentAt);
            Assert.Equal(NotificationLogStatuses.Created, previous.Status);
            Assert.Null(previous.SentAt);
        }

        [Fact]
        public async Task MarkOverdueNotificationLogsMissedAsync_MarksOnlyOpenLogsBeforeGraceCutoff()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteNotificationLogService(context, new TimeZoneService());
            var schedule = NewSchedule();
            var sentSchedule = NewSchedule(scheduleId: 2, cardId: 202);

            await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(12, 40), Utc(9));
            await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(12, 50), Utc(9, 5));
            await service.UpsertNotificationLogCreatedAsync(sentSchedule, "Break", Utc(12, 30), Utc(9, 10));
            await service.MarkNotificationLogSentAsync(sentSchedule, "Break", Utc(12, 30), Utc(12, 31));

            await service.MarkOverdueNotificationLogsMissedAsync(Utc(13), TimeSpan.FromMinutes(15));

            var logs = await service.GetNotificationLogsAsync();
            Assert.Equal(NotificationLogStatuses.Missed, Assert.Single(logs, x => x.ScheduleFor == Utc(12, 40)).Status);
            Assert.Equal(NotificationLogStatuses.Created, Assert.Single(logs, x => x.ScheduleFor == Utc(12, 50)).Status);
            Assert.Equal(NotificationLogStatuses.Sent, Assert.Single(logs, x => x.ScheduleFor == Utc(12, 30)).Status);
        }

        [Fact]
        public async Task GetNotificationLogsAsync_ClampsLimitAndOrdersNewestFirst()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteNotificationLogService(context, new TimeZoneService());
            var schedule = NewSchedule();

            await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(10), Utc(8));
            await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(11), Utc(8, 30));
            await service.UpsertNotificationLogCreatedAsync(schedule, "Focus", Utc(12), Utc(9));

            var logs = await service.GetNotificationLogsAsync(2);
            var clamped = await service.GetNotificationLogsAsync(0);

            Assert.Collection(
                logs,
                first => Assert.Equal(Utc(12), first.ScheduleFor),
                second => Assert.Equal(Utc(11), second.ScheduleFor));
            Assert.Single(clamped);
            Assert.Equal(Utc(12), clamped[0].ScheduleFor);
        }

        private static CardSchedule NewSchedule(long scheduleId = 1, long cardId = 101, string? note = "Reminder")
        {
            return new CardSchedule
            {
                ScheduleId = scheduleId,
                CardId = cardId,
                FrequencyType = FrequencyType.Once,
                FrequencyValue = 1,
                FromDateTime = Utc(12),
                ToDateTime = null,
                IsEnabled = true,
                Note = note
            };
        }

        private static DateTime Utc(int hour, int minute = 0)
        {
            return new DateTime(2026, 4, 29, hour, minute, 0, DateTimeKind.Utc);
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsNotificationLogServiceTests-{Guid.NewGuid():N}.db");
            }

            public string DatabasePath { get; }

            public SQLiteAsyncConnection Db => _db ?? throw new InvalidOperationException("DB not initialized.");

            public async Task InitializeAsync()
            {
                if (_db != null)
                    return;

                Batteries_V2.Init();

                _db = new SQLiteAsyncConnection(
                    DatabasePath,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

                await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS NotificationLog (
                        NotificationLogId INTEGER PRIMARY KEY AUTOINCREMENT,
                        ScheduleId INTEGER NOT NULL,
                        CardId INTEGER NOT NULL,
                        CardTitle TEXT NOT NULL DEFAULT '',
                        Note TEXT NOT NULL DEFAULT '',
                        Status TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        ScheduledAt TEXT NULL,
                        ScheduleFor TEXT NOT NULL,
                        SentAt TEXT NULL,
                        UpdatedAt TEXT NOT NULL,
                        Error TEXT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS UX_NotificationLog_ScheduleOccurrence
                    ON NotificationLog(ScheduleId, ScheduleFor);
                    """);
            }

            public async Task CloseDatabaseAsync()
            {
                if (_db == null)
                    return;

                await _db.CloseAsync();
                _db = null;
            }

            public async Task ReinitializeDatabaseAsync()
            {
                await CloseDatabaseAsync();
                await InitializeAsync();
            }

            public async Task RunInTransactionAsync(Action<SQLiteConnection> action)
            {
                if (action == null)
                    throw new ArgumentNullException(nameof(action));

                await InitializeAsync();

                await Db.RunInTransactionAsync(conn =>
                {
                    conn.Execute("PRAGMA foreign_keys = ON;");
                    action(conn);
                });
            }

            public async ValueTask DisposeAsync()
            {
                await CloseDatabaseAsync();

                if (File.Exists(DatabasePath))
                    File.Delete(DatabasePath);
            }
        }
    }
}
