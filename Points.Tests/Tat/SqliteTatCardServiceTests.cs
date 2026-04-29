using Points.Models;
using Points.Services.Schedules;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Tat;
using Points.Services.Time;
using Points.Tests.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Tat;

public sealed class SqliteTatCardServiceTests
{
    [Fact]
    public async Task SaveTatModelDataAsync_InsertsAndLoadsTatWithValueRatesActivityAndSchedules()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Deep Work", "focus");
        var tat = new TatCardModel
        {
            ValuePerMinute = 2.5,
            Status = "In-Progress",
            Description = "Writing block",
            TargetActiveTime = TimeSpan.FromMinutes(45)
        };
        tat.ValueRates.Add(new ValueRateModel { RateName = "Deep", ValuePerMinute = 4 });
        tat.ValueRates.Add(new ValueRateModel { RateName = "Admin", ValuePerMinute = 1 });
        tat.Schedules.Add(new CardSchedule
        {
            FrequencyType = FrequencyType.EveryDays,
            FrequencyValue = 1,
            FromDateTime = Local(2026, 4, 29, 9),
            IsEnabled = true,
            Note = "Start focus"
        });

        await service.SaveTatModelDataAsync(tat, cardId);
        await context.InsertActivityAsync(cardId, Utc(2026, 4, 29, 9), Utc(2026, 4, 29, 10), "Deep", 4);

        Assert.True(tat.Id > 0);
        Assert.All(tat.ValueRates, valueRate => Assert.True(valueRate.Id > 0));
        Assert.All(tat.Schedules, schedule => Assert.True(schedule.ScheduleId > 0));

        var row = Assert.Single(await context.GetTatRowsAsync());
        Assert.Equal(cardId, row.CardID);
        Assert.Equal(2.5, row.ValuePerMinute);
        Assert.Equal("In-Progress", row.Status);
        Assert.Equal("Writing block", row.Description);
        Assert.Equal(2700, row.TargetActiveTimeSeconds);

        var loaded = await service.GetTatModelDataAsync(tat.Id);

        Assert.Equal("Deep Work", loaded.Title);
        Assert.Equal("focus", loaded.Tags);
        Assert.Equal(TimeSpan.FromMinutes(45), loaded.TargetActiveTime);
        Assert.Equal(new[] { "Deep", "Admin" }, loaded.ValueRates.Select(x => x.RateName));

        var activity = Assert.Single(loaded.Activity);
        Assert.Equal(Utc(2026, 4, 29, 9), activity.StartDate);
        Assert.Equal(Utc(2026, 4, 29, 10), activity.EndDate);
        Assert.Equal("Deep", activity.RateName);

        var schedule = Assert.Single(loaded.Schedules);
        Assert.Equal(cardId, schedule.CardId);
        Assert.Equal(FrequencyType.EveryDays, schedule.FrequencyType);
        Assert.Equal("Start focus", schedule.Note);
    }

    [Fact]
    public async Task SaveTatModelDataAsync_UpdatesTatAndSyncsValueRateDeletes()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Practice", "music");
        var tat = new TatCardModel
        {
            ValuePerMinute = 1,
            Status = "In-Progress",
            Description = "Original",
            TargetActiveTime = TimeSpan.FromMinutes(30)
        };
        tat.ValueRates.Add(new ValueRateModel { RateName = "Warmup", ValuePerMinute = 1 });
        tat.ValueRates.Add(new ValueRateModel { RateName = "Scales", ValuePerMinute = 2 });
        await service.SaveTatModelDataAsync(tat, cardId);

        var removedRateId = tat.ValueRates[0].Id;
        tat.ValuePerMinute = 3;
        tat.Status = "Paused";
        tat.Description = "Updated";
        tat.TargetActiveTime = TimeSpan.FromMinutes(60);
        tat.ValueRates.RemoveAt(0);
        tat.ValueRates[0].RateName = "Etudes";
        tat.ValueRates[0].ValuePerMinute = 5;
        tat.ValueRates.Add(new ValueRateModel { RateName = "Sight reading", ValuePerMinute = 4 });

        await service.SaveTatModelDataAsync(tat, cardId);

        var row = Assert.Single(await context.GetTatRowsAsync());
        Assert.Equal(3, row.ValuePerMinute);
        Assert.Equal("Paused", row.Status);
        Assert.Equal("Updated", row.Description);
        Assert.Equal(3600, row.TargetActiveTimeSeconds);

        var valueRates = await context.GetValueRateRowsAsync(tat.Id);
        Assert.Equal(new[] { "Etudes", "Sight reading" }, valueRates.Select(x => x.RateName));
        Assert.DoesNotContain(valueRates, x => x.TatCardValueRateID == removedRateId);
    }

    [Fact]
    public async Task GetTatModelsDataAsync_FiltersActivityByRangeAndLoadsValueRates()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var firstCardId = await context.InsertCardAsync("Focused", "focus");
        var secondCardId = await context.InsertCardAsync("Admin", "admin");
        var first = new TatCardModel
        {
            ValuePerMinute = 2,
            Status = "In-Progress",
            Description = "In range"
        };
        first.ValueRates.Add(new ValueRateModel { RateName = "Deep", ValuePerMinute = 4 });
        var second = new TatCardModel
        {
            ValuePerMinute = 1,
            Status = "In-Progress",
            Description = "Out of range"
        };

        await service.SaveTatModelDataAsync(first, firstCardId);
        await service.SaveTatModelDataAsync(second, secondCardId);
        await context.InsertActivityAsync(firstCardId, Utc(2026, 4, 29, 10), Utc(2026, 4, 29, 11), "Deep", 4);
        await context.InsertActivityAsync(firstCardId, Utc(2026, 4, 28, 10), Utc(2026, 4, 28, 11), "Deep", 4);
        await context.InsertActivityAsync(secondCardId, Utc(2026, 4, 30, 10), Utc(2026, 4, 30, 11), "Admin", 1);

        var loaded = await service.GetTatModelsDataAsync(
            Utc(2026, 4, 29, 0),
            Utc(2026, 4, 29, 23, 59));

        Assert.Equal(2, loaded.Count);
        var focused = loaded.Single(x => x.Id == first.Id);
        var admin = loaded.Single(x => x.Id == second.Id);

        Assert.Single(focused.ValueRates);
        var activity = Assert.Single(focused.Activity);
        Assert.Equal(Utc(2026, 4, 29, 10), activity.StartDate);
        Assert.Empty(admin.Activity);
    }

    private static SqliteTatCardService CreateService(TestSqliteConnectionContext context)
    {
        return new SqliteTatCardService(
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
                $"PointsTatServiceTests-{Guid.NewGuid():N}.db");
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
                CREATE TABLE IF NOT EXISTS TatCard (
                    TatCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    ValuePerMinute REAL NOT NULL,
                    Status TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    TargetActiveTimeSeconds INTEGER NULL,
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS TatCardValueRate (
                    TatCardValueRateID INTEGER PRIMARY KEY AUTOINCREMENT,
                    TatCardID INTEGER NOT NULL,
                    RateName TEXT NOT NULL DEFAULT '',
                    ValuePerMinute REAL NOT NULL,
                    FOREIGN KEY(TatCardID) REFERENCES TatCard(TatCardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS Activity (
                    ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Start TEXT NOT NULL,
                    "End" TEXT NULL,
                    ValueRateName TEXT NOT NULL DEFAULT '',
                    ValuePerMinute REAL NOT NULL
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
        }

        public async Task<long> InsertCardAsync(string title, string tags)
        {
            await InitializeAsync();
            await Db.ExecuteAsync("INSERT INTO Card (Title, Tags) VALUES (?, ?);", title, tags);
            return await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        public async Task InsertActivityAsync(
            long cardId,
            DateTime startUtc,
            DateTime? endUtc,
            string rateName,
            double valuePerMinute)
        {
            await InitializeAsync();
            await Db.ExecuteAsync(
                @"INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                  VALUES (?, ?, ?, ?, ?);",
                cardId,
                StrictTimeSerializer.SerializeUtcInstant(startUtc),
                endUtc.HasValue ? StrictTimeSerializer.SerializeUtcInstant(endUtc.Value) : null,
                rateName,
                valuePerMinute);
        }

        public async Task<List<TatRow>> GetTatRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<TatRow>(
                @"SELECT TatCardID, CardID, ValuePerMinute, Status, Description, TargetActiveTimeSeconds
                  FROM TatCard
                  ORDER BY TatCardID;");
        }

        public async Task<List<ValueRateRow>> GetValueRateRowsAsync(long tatCardId)
        {
            await InitializeAsync();
            return await Db.QueryAsync<ValueRateRow>(
                @"SELECT TatCardValueRateID, TatCardID, RateName, ValuePerMinute
                  FROM TatCardValueRate
                  WHERE TatCardID = ?
                  ORDER BY TatCardValueRateID;",
                tatCardId);
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

    public sealed class TatRow
    {
        public long TatCardID { get; set; }
        public long CardID { get; set; }
        public double ValuePerMinute { get; set; }
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public int? TargetActiveTimeSeconds { get; set; }
    }

    public sealed class ValueRateRow
    {
        public long TatCardValueRateID { get; set; }
        public long TatCardID { get; set; }
        public string RateName { get; set; } = "";
        public double ValuePerMinute { get; set; }
    }
}
