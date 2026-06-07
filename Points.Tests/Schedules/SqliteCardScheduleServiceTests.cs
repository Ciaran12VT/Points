using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Schedules;
using Points.Services.Persistence;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Schedules
{
    public sealed class SqliteCardScheduleServiceTests
    {
        [Fact]
        public async Task SaveCardSchedulesAsync_InsertsSchedulesAndAssignsIds()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteCardScheduleService(context);
            var schedule = NewSchedule(from: Local(9, 30), to: null, note: "Morning");

            await service.SaveCardSchedulesAsync(101, new[] { schedule });

            Assert.True(schedule.ScheduleId > 0);
            Assert.Equal(101, schedule.CardId);

            var row = Assert.Single(await context.GetRowsAsync(101));
            Assert.Equal(schedule.ScheduleId, row.ScheduleID);
            Assert.Equal("EveryDays", row.FrequencyType);
            Assert.Equal("2026-04-29T09:30:00.0000000", row.FromDateTime);
            Assert.Null(row.ToDateTime);
            Assert.Equal(1, row.IsEnabled);
            Assert.Equal("Morning", row.Note);
        }

        [Fact]
        public async Task SaveCardSchedulesAsync_UpdatesExistingAndDeletesRemovedForSameCardOnly()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteCardScheduleService(context);
            var keep = NewSchedule(note: "Keep");
            var remove = NewSchedule(from: Local(11), note: "Remove");
            var otherCard = NewSchedule(from: Local(12), note: "Other");

            await service.SaveCardSchedulesAsync(101, new[] { keep, remove });
            await service.SaveCardSchedulesAsync(202, new[] { otherCard });

            keep.Note = "Updated";
            keep.IsEnabled = false;
            keep.FrequencyType = FrequencyType.EveryWeeks;
            keep.FrequencyValue = 2;
            keep.FromDateTime = Local(13);
            keep.ToDateTime = Local(14);

            await service.SaveCardSchedulesAsync(101, new[] { keep });

            var cardRows = await context.GetRowsAsync(101);
            var saved = Assert.Single(cardRows);

            Assert.Equal(keep.ScheduleId, saved.ScheduleID);
            Assert.Equal("Updated", saved.Note);
            Assert.Equal(0, saved.IsEnabled);
            Assert.Equal("EveryWeeks", saved.FrequencyType);
            Assert.Equal(2, saved.FrequencyValue);
            Assert.Equal("2026-04-29T13:00:00.0000000", saved.FromDateTime);
            Assert.Equal("2026-04-29T14:00:00.0000000", saved.ToDateTime);

            Assert.Single(await context.GetRowsAsync(202));
        }

        [Fact]
        public async Task GetCardSchedulesForCardAsync_ReturnsOnlyRequestedCardOrderedByFromDateTime()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteCardScheduleService(context);

            await context.InsertRowAsync(101, FrequencyType.EveryDays, 1, LocalText(12), null, true, "Later");
            await context.InsertRowAsync(202, FrequencyType.EveryDays, 1, LocalText(8), null, true, "Other");
            await context.InsertRowAsync(101, FrequencyType.EveryMonday, 1, LocalText(9), LocalText(10), false, "Earlier");

            var schedules = await service.GetCardSchedulesForCardAsync(101);

            Assert.Collection(
                schedules,
                first =>
                {
                    Assert.Equal(FrequencyType.EveryMonday, first.FrequencyType);
                    Assert.Equal(Local(9), first.FromDateTime);
                    Assert.Equal(Local(10), first.ToDateTime);
                    Assert.False(first.IsEnabled);
                },
                second =>
                {
                    Assert.Equal(FrequencyType.EveryDays, second.FrequencyType);
                    Assert.Equal(Local(12), second.FromDateTime);
                    Assert.True(second.IsEnabled);
                });
        }

        [Fact]
        public async Task GetEnabledCardSchedulesAsync_ReturnsOnlyEnabledOrderedByFromDateTime()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteCardScheduleService(context);

            await context.InsertRowAsync(101, FrequencyType.EveryDays, 1, LocalText(12), null, true, "Later");
            await context.InsertRowAsync(102, FrequencyType.EveryDays, 1, LocalText(8), null, false, "Disabled");
            await context.InsertRowAsync(103, FrequencyType.EveryMonday, 1, LocalText(9), null, true, "Earlier");

            var schedules = await service.GetEnabledCardSchedulesAsync();

            Assert.Collection(
                schedules,
                first => Assert.Equal(103, first.CardId),
                second => Assert.Equal(101, second.CardId));
        }

        [Fact]
        public async Task GetCardScheduleByIdAsync_ReturnsMappedScheduleOrNull()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteCardScheduleService(context);
            var id = await context.InsertRowAsync(101, FrequencyType.EveryFriday, 3, LocalText(9), LocalText(10), true, "By id");

            var schedule = await service.GetCardScheduleByIdAsync(id);
            var missing = await service.GetCardScheduleByIdAsync(9999);

            Assert.NotNull(schedule);
            Assert.Equal(id, schedule!.ScheduleId);
            Assert.Equal(101, schedule.CardId);
            Assert.Equal(FrequencyType.EveryFriday, schedule.FrequencyType);
            Assert.Equal(3, schedule.FrequencyValue);
            Assert.Equal("By id", schedule.Note);
            Assert.Null(missing);
        }

        private static CardSchedule NewSchedule(DateTime? from = null, DateTime? to = null, string? note = "Reminder")
        {
            return new CardSchedule
            {
                CardId = 0,
                FrequencyType = FrequencyType.EveryDays,
                FrequencyValue = 1,
                FromDateTime = from ?? Local(9),
                ToDateTime = to,
                IsEnabled = true,
                Note = note
            };
        }

        private static DateTime Local(int hour, int minute = 0)
        {
            return new DateTime(2026, 4, 29, hour, minute, 0, DateTimeKind.Unspecified);
        }

        private static string LocalText(int hour, int minute = 0)
        {
            return Local(hour, minute).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff");
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsCardScheduleServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS CardSchedule (
                        ScheduleID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardID INTEGER NOT NULL,
                        FrequencyType TEXT NOT NULL,
                        FrequencyValue INTEGER NOT NULL,
                        FromDateTime TEXT NOT NULL,
                        ToDateTime TEXT NULL,
                        IsEnabled INTEGER NOT NULL DEFAULT 1,
                        Note TEXT NOT NULL DEFAULT ''
                    );
                    """);
            }

            public async Task<long> InsertRowAsync(
                long cardId,
                FrequencyType frequencyType,
                int frequencyValue,
                string fromDateTime,
                string? toDateTime,
                bool isEnabled,
                string note)
            {
                await InitializeAsync();

                await Db.ExecuteAsync(
                    @"INSERT INTO CardSchedule
                      (CardID, FrequencyType, FrequencyValue, FromDateTime, ToDateTime, IsEnabled, Note)
                      VALUES (?, ?, ?, ?, ?, ?, ?);",
                    cardId,
                    frequencyType.ToString(),
                    frequencyValue,
                    fromDateTime,
                    toDateTime,
                    isEnabled ? 1 : 0,
                    note);

                return await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }

            public async Task<List<CardScheduleRow>> GetRowsAsync(long cardId)
            {
                await InitializeAsync();

                return await Db.QueryAsync<CardScheduleRow>(
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

        public sealed class CardScheduleRow
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
