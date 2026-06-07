using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Schedules;
using Points.Services.Sc;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Tests.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Sc;

public sealed class SqliteScCardServiceTests
{
    [Fact]
    public async Task SaveScModelDataAsync_InsertsAndLoadsScWithStepsRepsActivityAndSchedules()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Training", "health");
        var sc = new ScCardModel
        {
            Status = "In-Progress",
            Description = "Daily training",
            ValuePerMinute = 1
        };
        var step = new ScStepModel
        {
            SortOrder = 1,
            Title = "Pushups",
            StepValue = 2
        };
        step.Reps.Add(Utc(2026, 4, 29, 9));
        step.Reps.Add(Utc(2026, 4, 29, 10));
        sc.Steps.Add(step);
        sc.Schedules.Add(new CardSchedule
        {
            FrequencyType = FrequencyType.EveryDays,
            FrequencyValue = 1,
            FromDateTime = Local(2026, 4, 29, 8),
            IsEnabled = true,
            Note = "Train"
        });

        await service.SaveScModelDataAsync(sc, cardId);
        await context.InsertActivityAsync(cardId, Utc(2026, 4, 29, 8), Utc(2026, 4, 29, 8, 30), "Base", 1);

        Assert.True(sc.Id > 0);
        Assert.True(step.Id > 0);
        Assert.All(sc.Schedules, schedule => Assert.True(schedule.ScheduleId > 0));

        var scRow = Assert.Single(await context.GetScRowsAsync());
        Assert.Equal(cardId, scRow.CardID);
        Assert.Equal("In-Progress", scRow.Status);
        Assert.Equal("Daily training", scRow.Description);

        var loaded = await service.GetScModelDataAsync(sc.Id);

        Assert.Equal("Training", loaded.Title);
        Assert.Equal("health", loaded.Tags);
        Assert.Equal("Daily training", loaded.Description);

        var loadedStep = Assert.Single(loaded.Steps);
        Assert.Equal("Pushups", loadedStep.Title);
        Assert.Equal(new[] { Utc(2026, 4, 29, 9), Utc(2026, 4, 29, 10) }, loadedStep.Reps);

        var activity = Assert.Single(loaded.Activity);
        Assert.Equal(Utc(2026, 4, 29, 8), activity.StartDate);
        Assert.Equal(Utc(2026, 4, 29, 8, 30), activity.EndDate);

        var schedule = Assert.Single(loaded.Schedules);
        Assert.Equal(cardId, schedule.CardId);
        Assert.Equal(FrequencyType.EveryDays, schedule.FrequencyType);
        Assert.Equal("Train", schedule.Note);
    }

    [Fact]
    public async Task GetScModelsDataAsync_FiltersActivityAndRepsByRequestedRange()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var firstCardId = await context.InsertCardAsync("Morning", "health");
        var secondCardId = await context.InsertCardAsync("Evening", "health");

        var first = NewSc("Morning work");
        first.Steps[0].Reps.Add(Utc(2026, 4, 29, 9));
        first.Steps[0].Reps.Add(Utc(2026, 4, 28, 9));

        var second = NewSc("Evening work");
        second.Steps[0].Reps.Add(Utc(2026, 4, 30, 9));

        await service.SaveScModelDataAsync(first, firstCardId);
        await service.SaveScModelDataAsync(second, secondCardId);
        await context.InsertActivityAsync(firstCardId, Utc(2026, 4, 29, 10), Utc(2026, 4, 29, 11), "Base", 1);
        await context.InsertActivityAsync(firstCardId, Utc(2026, 4, 28, 10), Utc(2026, 4, 28, 11), "Base", 1);
        await context.InsertActivityAsync(secondCardId, Utc(2026, 4, 30, 10), Utc(2026, 4, 30, 11), "Base", 1);

        var loaded = await service.GetScModelsDataAsync(
            Utc(2026, 4, 29, 0),
            Utc(2026, 4, 29, 23, 59));

        Assert.Equal(2, loaded.Count);

        var morning = loaded.Single(x => x.Id == first.Id);
        var evening = loaded.Single(x => x.Id == second.Id);

        Assert.Equal(new[] { Utc(2026, 4, 29, 9) }, morning.Steps.Single().Reps);
        Assert.Single(morning.Activity);
        Assert.Empty(evening.Steps.Single().Reps);
        Assert.Empty(evening.Activity);
    }

    [Fact]
    public async Task RemoveRepForStepAsync_RemovesLatestRepAtOrBeforeCutoff()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Practice", "music");
        var sc = NewSc("Practice reps");
        sc.Steps[0].Reps.Add(Utc(2026, 4, 29, 9));
        sc.Steps[0].Reps.Add(Utc(2026, 4, 29, 10));
        sc.Steps[0].Reps.Add(Utc(2026, 4, 29, 11));
        await service.SaveScModelDataAsync(sc, cardId);

        await service.RemoveRepForStepAsync(sc.Steps[0].Id, Utc(2026, 4, 29, 10, 30));

        var loaded = await service.GetScModelDataAsync(sc.Id);
        Assert.Equal(
            new[] { Utc(2026, 4, 29, 9), Utc(2026, 4, 29, 11) },
            loaded.Steps.Single().Reps);
    }

    private static ScCardModel NewSc(string description)
    {
        var model = new ScCardModel
        {
            Status = "In-Progress",
            Description = description,
            ValuePerMinute = 1
        };

        model.Steps.Add(new ScStepModel
        {
            SortOrder = 1,
            Title = "Step",
            StepValue = 1
        });

        return model;
    }

    private static SqliteScCardService CreateService(TestSqliteConnectionContext context)
    {
        return new SqliteScCardService(
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
                $"PointsScServiceTests-{Guid.NewGuid():N}.db");
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
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    Title TEXT NOT NULL,
                    Tags TEXT NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ScCard (
                    ScCardID INTEGER PRIMARY KEY,
                    CardID INTEGER NOT NULL,
                    Status TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ScCardStep (
                    ScCardStepID INTEGER PRIMARY KEY,
                    ScCardID INTEGER NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    Title TEXT NOT NULL DEFAULT '',
                    StepValue REAL NOT NULL,
                    FOREIGN KEY(ScCardID) REFERENCES ScCard(ScCardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ScCardStepRep (
                    ScCardStepID INTEGER NOT NULL,
                    TimeStamp TEXT NOT NULL,
                    StepValue REAL NOT NULL,
                    PRIMARY KEY (ScCardStepID, TimeStamp),
                    FOREIGN KEY(ScCardStepID) REFERENCES ScCardStep(ScCardStepID) ON DELETE CASCADE
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

        public async Task<List<ScRow>> GetScRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<ScRow>(
                @"SELECT ScCardID, CardID, Status, Description
                  FROM ScCard
                  ORDER BY ScCardID;");
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

    public sealed class ScRow
    {
        public long ScCardID { get; set; }
        public long CardID { get; set; }
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
