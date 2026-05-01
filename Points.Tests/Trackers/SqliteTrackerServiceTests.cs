using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Schedules;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Services.Trackers;
using Points.Tests.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Trackers;

public sealed class SqliteTrackerServiceTests
{
    [Fact]
    public async Task SaveValueTrackerCardModelDataAsync_InsertsAndLoadsValueTrackerWithValuesAndSchedules()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Weight", "health");
        var tracker = new ValueTrackerCardModel
        {
            Unit = "kg",
            CreatedDate = Local(2026, 4, 1, 8),
            RangeStart = Local(2026, 4, 1),
            ScheduleEvery = 2,
            ScheduleUnit = "Week"
        };
        tracker.Values.Add(new TrackerValueModel
        {
            Timestamp = Utc(2026, 4, 29, 7, 30),
            Value = 71.5
        });
        tracker.Values.Add(new TrackerValueModel
        {
            Timestamp = Utc(2026, 4, 30, 7, 30),
            Value = 71.2
        });
        tracker.Schedules.Add(new CardSchedule
        {
            FrequencyType = FrequencyType.EveryWeeks,
            FrequencyValue = 1,
            FromDateTime = Local(2026, 4, 29, 9),
            IsEnabled = true,
            Note = "Weigh in"
        });

        await service.SaveValueTrackerCardModelDataAsync(tracker, cardId);

        Assert.True(tracker.Id > 0);
        Assert.All(tracker.Values, value => Assert.True(value.Id > 0));
        Assert.All(tracker.Schedules, schedule => Assert.True(schedule.ScheduleId > 0));

        var row = Assert.Single(await context.GetValueTrackerRowsAsync());
        Assert.Equal(cardId, row.CardID);
        Assert.Equal("kg", row.Unit);
        Assert.Equal("2026-04-01T08:00:00.0000000", row.CreatedDate);
        Assert.Equal("2026-04-01T00:00:00.0000000", row.RangeStart);
        Assert.Equal(2, row.ScheduleEvery);
        Assert.Equal("Week", row.ScheduleUnit);

        var loaded = await service.GetValueTrackerCardModelDataAsync(tracker.Id);

        Assert.Equal("Weight", loaded.Title);
        Assert.Equal("health", loaded.Tags);
        Assert.Equal(Local(2026, 4, 1, 8), loaded.CreatedDate);
        Assert.Equal(new[] { 71.5, 71.2 }, loaded.Values.Select(x => x.Value));
        Assert.Equal(Utc(2026, 4, 29, 7, 30), loaded.Values[0].Timestamp);

        var loadedSchedule = Assert.Single(loaded.Schedules);
        Assert.Equal(cardId, loadedSchedule.CardId);
        Assert.Equal(FrequencyType.EveryWeeks, loadedSchedule.FrequencyType);
        Assert.Equal("Weigh in", loadedSchedule.Note);

        var valueRow = Assert.Single(await context.GetTrackerValueRowsAsync(cardId), x => x.Value == 71.5);
        Assert.Equal("2026-04-29T07:30:00.0000000Z", valueRow.TimeStamp);
    }

    [Fact]
    public async Task SaveValueTrackerCardModelDataAsync_UpdatesAndDeletesRemovedValuesAndMetadata()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Mood", "journal");
        var tracker = new ValueTrackerCardModel
        {
            Unit = "score",
            CreatedDate = Local(2026, 4, 1),
            RangeStart = Local(2026, 4, 1)
        };
        tracker.Values.Add(new TrackerValueModel
        {
            Timestamp = Utc(2026, 4, 29, 9),
            Value = 3
        });
        tracker.Values.Add(new TrackerValueModel
        {
            Timestamp = Utc(2026, 4, 30, 9),
            Value = 4
        });
        await service.SaveValueTrackerCardModelDataAsync(tracker, cardId);

        var removedValueId = tracker.Values[0].Id;
        await context.InsertMetadataAsync(cardId, removedValueId);

        tracker.Unit = "points";
        tracker.Values.RemoveAt(0);
        tracker.Values[0].Value = 5;
        tracker.Values.Add(new TrackerValueModel
        {
            Timestamp = Utc(2026, 5, 1, 9),
            Value = 6
        });

        await service.SaveValueTrackerCardModelDataAsync(tracker, cardId);

        var row = Assert.Single(await context.GetValueTrackerRowsAsync());
        Assert.Equal("points", row.Unit);

        var values = await context.GetTrackerValueRowsAsync(cardId);
        Assert.Equal(new[] { 5d, 6d }, values.Select(x => x.Value));
        Assert.DoesNotContain(values, x => x.TrackerValueID == removedValueId);
        Assert.Empty(await context.GetMetadataRowsAsync(removedValueId));
    }

    [Fact]
    public async Task SaveEventTrackerCardModelDataAsync_InsertsLoadsAndFiltersEventTrackers()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var firstCardId = await context.InsertCardAsync("Headaches", "health");
        var secondCardId = await context.InsertCardAsync("Archived", "old");

        var first = new EventTrackerCardModel
        {
            Unit = "event",
            CreatedDate = Local(2026, 4, 1),
            RangeStart = Local(2026, 4, 1),
            GroupByPeriod = "Week"
        };
        first.Values.Add(new TrackerValueModel
        {
            Timestamp = Utc(2026, 4, 29, 10),
            Value = 1
        });

        var second = new EventTrackerCardModel
        {
            Unit = "event",
            CreatedDate = Local(2026, 4, 1),
            RangeStart = Local(2026, 4, 1),
            GroupByPeriod = "Month"
        };

        await service.SaveEventTrackerCardModelDataAsync(first, firstCardId);
        await service.SaveEventTrackerCardModelDataAsync(second, secondCardId);

        var loaded = await service.GetEventTrackerCardModelDataAsync(first.Id);

        Assert.Equal("Headaches", loaded.Title);
        Assert.Equal("Week", loaded.GroupByPeriod);
        var value = Assert.Single(loaded.Values);
        Assert.True(value.Id > 0);
        Assert.Equal(Utc(2026, 4, 29, 10), value.Timestamp);
        Assert.Equal(1, value.Value);

        var filtered = await service.GetEventTrackerCardModelsDataAsync("c.Tags = 'health'");
        var filteredTracker = Assert.Single(filtered);
        Assert.Equal(first.Id, filteredTracker.Id);
        Assert.Single(filteredTracker.Values);
    }

    private static SqliteTrackerService CreateService(TestSqliteConnectionContext context)
    {
        return new SqliteTrackerService(
            context,
            new FixedZoneTimeZoneService(TimeZoneInfo.Utc),
            new SqliteCardScheduleService(context));
    }

    private static DateTime Local(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }

    private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
    {
        private SQLiteAsyncConnection? _db;

        public TestSqliteConnectionContext()
        {
            DatabasePath = Path.Combine(
                Path.GetTempPath(),
                $"PointsTrackerServiceTests-{Guid.NewGuid():N}.db");
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
                CREATE TABLE IF NOT EXISTS Card (
                    CardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Tags TEXT NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ValueTrackerCard (
                    ValueTrackerCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Unit TEXT NOT NULL DEFAULT '',
                    CreatedDate TEXT NOT NULL,
                    RangeStart TEXT NOT NULL,
                    ScheduleEvery INTEGER NOT NULL DEFAULT 1,
                    ScheduleUnit TEXT NOT NULL DEFAULT 'Week',
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS EventTrackerCard (
                    EventTrackerCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Unit TEXT NOT NULL DEFAULT '',
                    CreatedDate TEXT NOT NULL,
                    RangeStart TEXT NOT NULL,
                    GroupByPeriod TEXT NOT NULL DEFAULT 'Day',
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS TrackerValue (
                    TrackerValueID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    TimeStamp TEXT NOT NULL,
                    Value REAL NOT NULL,
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
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
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS UdmdTrans (
                    UdmdTransID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    UdmdConfigID INTEGER NOT NULL,
                    RelatedEntityType TEXT NOT NULL,
                    RelatedEntityId INTEGER NOT NULL,
                    FieldValue TEXT NOT NULL
                );
                """);
        }

        public async Task<long> InsertCardAsync(string title, string tags)
        {
            await InitializeAsync();
            await Db.ExecuteAsync("INSERT INTO Card (Title, Tags) VALUES (?, ?);", title, tags);
            return await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        public async Task InsertMetadataAsync(long cardId, long trackerValueId)
        {
            await InitializeAsync();
            await Db.ExecuteAsync(
                @"INSERT INTO UdmdTrans (CardID, UdmdConfigID, RelatedEntityType, RelatedEntityId, FieldValue)
                  VALUES (?, ?, ?, ?, ?);",
                cardId,
                1,
                UdmdRelatedEntityTypes.TrackerValue,
                trackerValueId,
                "note");
        }

        public async Task<List<ValueTrackerRow>> GetValueTrackerRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<ValueTrackerRow>(
                @"SELECT ValueTrackerCardID, CardID, Unit, CreatedDate, RangeStart, ScheduleEvery, ScheduleUnit
                  FROM ValueTrackerCard
                  ORDER BY ValueTrackerCardID;");
        }

        public async Task<List<TrackerValueRow>> GetTrackerValueRowsAsync(long cardId)
        {
            await InitializeAsync();
            return await Db.QueryAsync<TrackerValueRow>(
                @"SELECT TrackerValueID, CardID, TimeStamp, Value
                  FROM TrackerValue
                  WHERE CardID = ?
                  ORDER BY TimeStamp;",
                cardId);
        }

        public async Task<List<UdmdTransRow>> GetMetadataRowsAsync(long trackerValueId)
        {
            await InitializeAsync();
            return await Db.QueryAsync<UdmdTransRow>(
                @"SELECT UdmdTransID, RelatedEntityId
                  FROM UdmdTrans
                  WHERE RelatedEntityType = ?
                    AND RelatedEntityId = ?;",
                UdmdRelatedEntityTypes.TrackerValue,
                trackerValueId);
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

    public sealed class ValueTrackerRow
    {
        public long ValueTrackerCardID { get; set; }
        public long CardID { get; set; }
        public string Unit { get; set; } = "";
        public string CreatedDate { get; set; } = "";
        public string RangeStart { get; set; } = "";
        public int ScheduleEvery { get; set; }
        public string ScheduleUnit { get; set; } = "";
    }

    public sealed class TrackerValueRow
    {
        public long TrackerValueID { get; set; }
        public long CardID { get; set; }
        public string TimeStamp { get; set; } = "";
        public double Value { get; set; }
    }

    public sealed class UdmdTransRow
    {
        public long UdmdTransID { get; set; }
        public long RelatedEntityId { get; set; }
    }
}
