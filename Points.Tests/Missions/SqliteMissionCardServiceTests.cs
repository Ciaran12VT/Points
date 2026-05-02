using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Missions;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Tests.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Missions;

public sealed class SqliteMissionCardServiceTests
{
    [Fact]
    public async Task SaveMissionCardModelDataAsync_InsertsAndLoadsMissionWithActivity()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Launch work", "focus");
        var mission = new MissionCardModel
        {
            Status = "In-Progress",
            Description = "Ship the next slice",
            SubType = MissionSubType.Degrade,
            Value = 42,
            CreatedDate = Utc(2026, 4, 1, 8),
            AvailableFromDate = Local(2026, 4, 29, 9),
            DueDate = Local(2026, 4, 30, 17),
            EventDate = Local(2026, 4, 30, 16),
            EstCompletionTime = TimeSpan.FromMinutes(90),
            ValuePerMinute = 1.5
        };

        await service.SaveMissionCardModelDataAsync(mission, cardId);
        await context.InsertActivityAsync(cardId, Utc(2026, 4, 29, 9), Utc(2026, 4, 29, 10), "Mission", 1.5);

        Assert.True(mission.Id > 0);

        var row = Assert.Single(await context.GetMissionRowsAsync());
        Assert.Equal(cardId, row.CardID);
        Assert.Equal("In-Progress", row.Status);
        Assert.Equal("Ship the next slice", row.Description);
        Assert.Equal("Degrade", row.SubType);
        Assert.Equal("1:30:00", row.EstCompletionTimeText);

        var loaded = await service.GetMissionCardModelDataAsync(mission.Id);

        Assert.Equal("Launch work", loaded.Title);
        Assert.Equal("focus", loaded.Tags);
        Assert.Equal("Ship the next slice", loaded.Description);
        Assert.Equal(MissionSubType.Degrade, loaded.SubType);
        Assert.Equal(42, loaded.Value);
        Assert.Equal(Local(2026, 4, 29, 9), loaded.AvailableFromDate);
        Assert.Equal(Local(2026, 4, 30, 17), loaded.DueDate);
        Assert.Equal(Local(2026, 4, 30, 16), loaded.EventDate);
        Assert.Equal(TimeSpan.FromMinutes(90), loaded.EstCompletionTime);

        var activity = Assert.Single(loaded.Activity);
        Assert.Equal(Utc(2026, 4, 29, 9), activity.StartDate);
        Assert.Equal(Utc(2026, 4, 29, 10), activity.EndDate);
        Assert.Equal("Mission", activity.RateName);
    }

    [Fact]
    public async Task SaveMissionCardModelDataAsync_UpdatesMissionMetadata()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Quest", "main");
        var createdAt = Utc(2026, 4, 1, 8);
        var mission = new MissionCardModel
        {
            Status = "In-Progress",
            Description = "Original",
            SubType = MissionSubType.Stable,
            Value = 10,
            CreatedDate = createdAt,
            AvailableFromDate = Local(2026, 4, 29, 9),
            DueDate = Local(2026, 4, 30, 17),
            ValuePerMinute = 1
        };
        await service.SaveMissionCardModelDataAsync(mission, cardId);

        mission.Status = "Paused";
        mission.Description = "Updated";
        mission.SubType = MissionSubType.Rot;
        mission.Value = 25;
        mission.AvailableFromDate = Local(2026, 5, 1, 9);
        mission.DueDate = Local(2026, 5, 2, 17);
        mission.EventDate = Local(2026, 5, 2, 12);
        mission.EstCompletionTime = TimeSpan.FromHours(2);
        mission.ValuePerMinute = 3;

        await service.SaveMissionCardModelDataAsync(mission, cardId);

        var row = Assert.Single(await context.GetMissionRowsAsync());
        Assert.Equal(createdAt.ToString("o"), row.CreatedDate);
        Assert.Equal("Paused", row.Status);
        Assert.Equal("Updated", row.Description);
        Assert.Equal("Rot", row.SubType);
        Assert.Equal(25, row.Value);
        Assert.Equal(Local(2026, 5, 1, 9).ToString("o"), row.AvailableFromDate);
        Assert.Equal(Local(2026, 5, 2, 17).ToString("o"), row.DueDate);
        Assert.Equal(Local(2026, 5, 2, 12).ToString("o"), row.EventDate);
        Assert.Equal("2:00:00", row.EstCompletionTimeText);
        Assert.Equal(3, row.ValuePerMinute);
    }

    [Fact]
    public async Task GetMissionCardModelsDataAsync_AppliesWhereClauseAndRestoresCompletionState()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var openCardId = await context.InsertCardAsync("Open", "mission");
        var completedCardId = await context.InsertCardAsync("Completed", "mission");
        var failedCardId = await context.InsertCardAsync("Failed", "mission");

        await service.SaveMissionCardModelDataAsync(NewMission(10), openCardId);

        var completed = NewMission(50);
        completed.Complete(Utc(2026, 4, 29, 12));
        await service.SaveMissionCardModelDataAsync(completed, completedCardId);

        var failed = NewMission(100);
        failed.Fail(Utc(2026, 4, 29, 13));
        await service.SaveMissionCardModelDataAsync(failed, failedCardId);

        var loaded = await service.GetMissionCardModelsDataAsync("m.Value >= 50 ORDER BY m.MissionCardID");

        Assert.Equal(2, loaded.Count);

        var loadedCompleted = loaded.Single(x => x.CardID == completedCardId);
        Assert.True(loadedCompleted.IsComplete);
        Assert.False(loadedCompleted.IsFailed);
        Assert.Equal("Complete", loadedCompleted.Status);
        Assert.Equal(Utc(2026, 4, 29, 12), loadedCompleted.CompletedDate);

        var loadedFailed = loaded.Single(x => x.CardID == failedCardId);
        Assert.True(loadedFailed.IsComplete);
        Assert.True(loadedFailed.IsFailed);
        Assert.Equal("Failed", loadedFailed.Status);
        Assert.Equal(Utc(2026, 4, 29, 13), loadedFailed.CompletedDate);
    }

    private static MissionCardModel NewMission(double value)
    {
        return new MissionCardModel
        {
            Status = "In-Progress",
            Description = "Mission",
            SubType = MissionSubType.Stable,
            Value = value,
            CreatedDate = Utc(2026, 4, 1, 8),
            AvailableFromDate = Local(2026, 4, 29, 9),
            DueDate = Local(2026, 4, 30, 17),
            ValuePerMinute = 1
        };
    }

    private static SqliteMissionCardService CreateService(TestSqliteConnectionContext context)
    {
        return new SqliteMissionCardService(context, new FixedZoneTimeZoneService(TimeZoneInfo.Utc));
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
                $"PointsMissionServiceTests-{Guid.NewGuid():N}.db");
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
                CREATE TABLE IF NOT EXISTS MissionCard (
                    MissionCardID INTEGER PRIMARY KEY,
                    CardID INTEGER NOT NULL,
                    Status TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    SubType TEXT NOT NULL DEFAULT '',
                    Value REAL NOT NULL,
                    CreatedDate TEXT NOT NULL,
                    AvailableFromDate TEXT NOT NULL,
                    DueDate TEXT NOT NULL,
                    CompletedDate TEXT NULL,
                    EventDate TEXT NULL,
                    EstCompletionTimeText TEXT NOT NULL DEFAULT '',
                    IsFailed INTEGER NOT NULL DEFAULT 0,
                    ValuePerMinute REAL NOT NULL,
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
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

        public async Task<List<MissionRow>> GetMissionRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<MissionRow>(
                @"SELECT MissionCardID,
                         CardID,
                         Status,
                         Description,
                         SubType,
                         Value,
                         CreatedDate,
                         AvailableFromDate,
                         DueDate,
                         CompletedDate,
                         EventDate,
                         EstCompletionTimeText,
                         IsFailed,
                         ValuePerMinute
                  FROM MissionCard
                  ORDER BY MissionCardID;");
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

    public sealed class MissionRow
    {
        public long MissionCardID { get; set; }
        public long CardID { get; set; }
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public string SubType { get; set; } = "";
        public double Value { get; set; }
        public string CreatedDate { get; set; } = "";
        public string AvailableFromDate { get; set; } = "";
        public string DueDate { get; set; } = "";
        public string? CompletedDate { get; set; }
        public string? EventDate { get; set; }
        public string EstCompletionTimeText { get; set; } = "";
        public int IsFailed { get; set; }
        public double ValuePerMinute { get; set; }
    }
}
