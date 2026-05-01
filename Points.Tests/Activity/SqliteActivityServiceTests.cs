using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Activity;
using Points.Services.Persistence;
using Points.Services.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Activity
{
    public sealed class SqliteActivityServiceTests
    {
        [Fact]
        public async Task ToggleActivityAsync_OpensAndClosesSameCard()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteActivityService(context, new TimeZoneService());

            var opened = await service.ToggleActivityAsync(101, Utc(9), "Base Rate", 2);

            Assert.Null(opened.Closed);
            Assert.NotNull(opened.Opened);
            Assert.Equal(101, opened.Opened!.CardID);
            Assert.Equal(Utc(9), opened.Opened.StartDate);
            Assert.Null(opened.Opened.EndDate);
            Assert.Equal(Utc(9), await service.GetCurrentOpenActivityStartUtcAsync(101));
            Assert.Equal(101, (await service.GetCurrentActiveActivityAsync())!.CardID);

            var closed = await service.ToggleActivityAsync(101, Utc(10), "Base Rate", 2);

            Assert.NotNull(closed.Closed);
            Assert.Null(closed.Opened);
            Assert.Equal(Utc(10), closed.Closed!.EndDate);
            Assert.Null(await service.GetCurrentActiveActivityAsync());
            Assert.Equal(Utc(10), await service.GetLastClosedActivityEndUtcAsync());
        }

        [Fact]
        public async Task ToggleActivityAsync_ClosesDifferentOpenCardAndOpensRequestedCard()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteActivityService(context, new TimeZoneService());

            await service.ToggleActivityAsync(101, Utc(9), "Base Rate", 1);
            var result = await service.ToggleActivityAsync(202, Utc(10), "Focus", 3);

            Assert.Equal(101, result.Closed!.CardID);
            Assert.Equal(Utc(10), result.Closed.EndDate);
            Assert.Equal(202, result.Opened!.CardID);
            Assert.Equal("Focus", result.Opened.RateName);

            var current = await service.GetCurrentActiveActivityAsync();
            Assert.Equal(202, current!.CardID);
            Assert.Equal(Utc(10), current.StartDate);
        }

        [Fact]
        public async Task HasActivityOverlapAsync_DetectsOverlapsAndHonorsExcludedActivity()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteActivityService(context, new TimeZoneService());

            await service.UpsertActivitiesAsync(new List<ActivityModel>
            {
                NewActivity(101, Utc(9), Utc(10))
            });
            var existing = Assert.Single(await context.GetActivitiesAsync());

            Assert.True(await service.HasActivityOverlapAsync(0, Utc(9).AddMinutes(30), Utc(9).AddMinutes(45)));
            Assert.False(await service.HasActivityOverlapAsync(0, Utc(10), Utc(11)));
            Assert.False(await service.HasActivityOverlapAsync(existing.ActivityID, Utc(9).AddMinutes(30), Utc(9).AddMinutes(45)));
        }

        [Fact]
        public async Task UpsertActivitiesAsync_ReplacesOnlyRequestedCard()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteActivityService(context, new TimeZoneService());

            await service.UpsertActivitiesAsync(new List<ActivityModel>
            {
                NewActivity(101, Utc(9), Utc(10)),
                NewActivity(202, Utc(12), Utc(13))
            });

            var result = await service.UpsertActivitiesAsync(
                new List<ActivityModel> { NewActivity(101, Utc(14), Utc(15)) },
                replaceCardId: 101);

            Assert.True(result.Success);

            var activities = await context.GetActivitiesAsync();
            Assert.Equal(2, activities.Count);
            Assert.Contains(activities, x => x.CardID == 101 && x.Start == "2026-04-29T14:00:00.0000000Z");
            Assert.Contains(activities, x => x.CardID == 202 && x.Start == "2026-04-29T12:00:00.0000000Z");
        }

        [Fact]
        public async Task UpsertActivitiesAsync_RejectsInternalOverlap()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteActivityService(context, new TimeZoneService());

            var result = await service.UpsertActivitiesAsync(new List<ActivityModel>
            {
                NewActivity(101, Utc(9), Utc(10)),
                NewActivity(101, Utc(9).AddMinutes(30), Utc(11))
            });

            Assert.False(result.Success);
            Assert.Contains("Overlapping Activities", result.Message);
            Assert.Empty(await context.GetActivitiesAsync());
        }

        [Fact]
        public async Task AddRepForStep_UpsertsStepRep()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteActivityService(context, new TimeZoneService());

            await service.AddRepForStep(7, Utc(9), 1);
            await service.AddRepForStep(7, Utc(9), 2);

            var rep = Assert.Single(await context.GetRepsAsync());
            Assert.Equal(7, rep.ScCardStepID);
            Assert.Equal("2026-04-29T09:00:00.0000000Z", rep.TimeStamp);
            Assert.Equal(2, rep.StepValue);
        }

        private static ActivityModel NewActivity(long cardId, DateTime startUtc, DateTime? endUtc)
        {
            return new ActivityModel
            {
                CardID = cardId,
                StartDate = startUtc,
                EndDate = endUtc,
                RateName = "Base Rate",
                ValuePerMinute = 1
            };
        }

        private static DateTime Utc(int hour)
        {
            return new DateTime(2026, 4, 29, hour, 0, 0, DateTimeKind.Utc);
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsActivityServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS Activity (
                        ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardID INTEGER NOT NULL,
                        Start TEXT NOT NULL,
                        "End" TEXT NULL,
                        ValueRateName TEXT NOT NULL,
                        ValuePerMinute REAL NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS ScCardStepRep (
                        ScCardStepID INTEGER NOT NULL,
                        TimeStamp TEXT NOT NULL,
                        StepValue REAL NOT NULL,
                        PRIMARY KEY (ScCardStepID, TimeStamp)
                    );
                    """);
            }

            public async Task<List<ActivityRow>> GetActivitiesAsync()
            {
                await InitializeAsync();
                return await Db.QueryAsync<ActivityRow>(
                    @"SELECT ActivityID, CardID, Start, ""End"", ValueRateName, ValuePerMinute
                      FROM Activity
                      ORDER BY ActivityID;");
            }

            public async Task<List<StepRepRow>> GetRepsAsync()
            {
                await InitializeAsync();
                return await Db.QueryAsync<StepRepRow>(
                    @"SELECT ScCardStepID, TimeStamp, StepValue
                      FROM ScCardStepRep
                      ORDER BY ScCardStepID, TimeStamp;");
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

        private sealed class ActivityRow
        {
            public int ActivityID { get; set; }
            public long CardID { get; set; }
            public string Start { get; set; } = "";
            public string? End { get; set; }
            public string ValueRateName { get; set; } = "";
            public double ValuePerMinute { get; set; }
        }

        private sealed class StepRepRow
        {
            public int ScCardStepID { get; set; }
            public string TimeStamp { get; set; } = "";
            public double StepValue { get; set; }
        }
    }
}
