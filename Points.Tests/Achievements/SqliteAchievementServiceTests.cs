using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Achievements;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Tests.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Achievements;

public sealed class SqliteAchievementServiceTests
{
    [Fact]
    public async Task SaveAchievementCardModelDataAsync_InsertsAndLoadsAchievement()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Deep Work", "focus");
        var achievement = new AchievementCardModel
        {
            Title = "Loaded from Card",
            Status = "In-Progress",
            Description = "Spend focused time.",
            Tags = "focus",
            TargetType = AchievementTargetType.ActiveTime,
            ActiveTimeTargetText = "1:30:00",
            Difficulty = AchievementDifficultyLevels.Hard,
            CompletionType = AchievementCompletionType.Range,
            RangeUnit = AchievementRangeUnit.Days,
            RangeAmount = 7,
            CreatedDate = Local(2026, 4, 1, 9),
            IsPinned = true
        };
        achievement.Trophies.Add("alpha.png");
        achievement.Trophies.Add("beta.png");

        await service.SaveAchievementCardModelDataAsync(achievement, cardId);

        Assert.True(achievement.Id > 0);
        var row = Assert.Single(await context.GetAchievementRowsAsync());
        Assert.Equal(cardId, row.CardID);
        Assert.Equal("Hard", row.DifficultyLevel);
        Assert.Equal(5400, row.TargetActiveTimeInSeconds);
        Assert.Equal("Days", row.RangeUnit);
        Assert.Equal(7, row.RangeAmount);
        Assert.Equal("2026-04-01T09:00:00.0000000", row.CreatedDate);
        Assert.Equal("alpha.png\nbeta.png", row.TrophyURLs);
        Assert.Equal(1, row.IsPinned);

        var loaded = await service.GetAchievementCardModelDataAsync(achievement.Id);

        Assert.Equal("Deep Work", loaded.Title);
        Assert.Equal("focus", loaded.Tags);
        Assert.Equal(AchievementTargetType.ActiveTime, loaded.TargetType);
        Assert.Equal("1:30:00", loaded.ActiveTimeTargetText);
        Assert.Equal(new[] { "alpha.png", "beta.png" }, loaded.Trophies);
    }

    [Fact]
    public async Task SaveAchievementCardModelDataAsync_UpdatesExistingAchievement()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Points", "value");
        var achievement = new AchievementCardModel
        {
            Status = "In-Progress",
            Tags = "value",
            TargetType = AchievementTargetType.Value,
            TargetValue = 5,
            CompletionType = AchievementCompletionType.Range,
            RangeUnit = AchievementRangeUnit.Days,
            RangeAmount = 1,
            CreatedDate = Local(2026, 4, 1)
        };

        await service.SaveAchievementCardModelDataAsync(achievement, cardId);

        achievement.Status = "Paused";
        achievement.Description = "Updated";
        achievement.TargetValue = 12;
        achievement.RangeUnit = AchievementRangeUnit.Weeks;
        achievement.RangeAmount = 2;
        achievement.IsPinned = true;

        await service.SaveAchievementCardModelDataAsync(achievement, cardId);

        var row = Assert.Single(await context.GetAchievementRowsAsync());
        Assert.Equal("Paused", row.Status);
        Assert.Equal("Updated", row.Description);
        Assert.Equal(12, row.TargetValue);
        Assert.Equal("Weeks", row.RangeUnit);
        Assert.Equal(2, row.RangeAmount);
        Assert.Equal(1, row.IsPinned);
    }

    [Fact]
    public async Task MarkAchievementEarnedAsync_UpdatesLastEarnedAndAwardsEligibleTrophy()
    {
        await using var context = new TestSqliteConnectionContext();
        using var trophyRoot = new TempDirectory();
        var service = CreateService(context, trophyRoot);
        var cardId = await context.InsertCardAsync("Earned", "value");
        var achievement = new AchievementCardModel
        {
            Status = "In-Progress",
            Tags = "value",
            TargetType = AchievementTargetType.Value,
            TargetValue = 1,
            CompletionType = AchievementCompletionType.Range,
            RangeUnit = AchievementRangeUnit.Days,
            RangeAmount = 1,
            CreatedDate = Local(2026, 4, 1)
        };
        await service.SaveAchievementCardModelDataAsync(achievement, cardId);

        var trophyFolder = trophyRoot.GetAchievementFolder(achievement.Id);
        File.WriteAllText(Path.Combine(trophyFolder, "base.png"), "");
        File.WriteAllText(Path.Combine(trophyFolder, "locked_base.png"), "");

        await service.MarkAchievementEarnedAsync(achievement.Id, Utc(2026, 4, 29, 10, 30));

        var row = Assert.Single(await context.GetAchievementRowsAsync());
        Assert.Equal("2026-04-29T10:30:00.0000000Z", row.LastEarnedAt);

        var trophy = Assert.Single(await context.GetTrophyRowsAsync());
        Assert.Equal(achievement.Id, trophy.AchievementCardID);
        Assert.Equal("base.png", trophy.ImageSource);
        Assert.Equal("base", trophy.Title);
        Assert.Equal("2026-04-29T10:30:00.0000000Z", trophy.EarnedOn);
    }

    [Fact]
    public async Task GetAchievementCardModelDataAsync_FinalizesDeadlineValueAchievement()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context, utcNow: Utc(2026, 4, 29, 10, 30));
        var achievementCardId = await context.InsertCardAsync("Deadline", "focus");
        var sourceCardId = await context.InsertCardAsync("Source", "focus");
        var achievement = new AchievementCardModel
        {
            Status = "In-Progress",
            Tags = "focus",
            TargetType = AchievementTargetType.Value,
            TargetValue = 5,
            CompletionType = AchievementCompletionType.Deadline,
            RangeUnit = AchievementRangeUnit.Days,
            RangeAmount = 1,
            CreatedDate = Local(2026, 4, 29, 8),
            DeadlineStart = Local(2026, 4, 29, 9),
            Deadline = Local(2026, 4, 29, 12)
        };
        await service.SaveAchievementCardModelDataAsync(achievement, achievementCardId);
        await context.InsertActivityAsync(
            sourceCardId,
            Utc(2026, 4, 29, 9),
            Utc(2026, 4, 29, 10),
            valuePerMinute: 1);

        var loaded = await service.GetAchievementCardModelDataAsync(achievement.Id);

        Assert.Equal("Completed", loaded.Status);
        Assert.Equal(Local(2026, 4, 29, 10, 30), loaded.FinalizedAt);
        Assert.Equal(60, loaded.FrozenCurrentValue);
        Assert.Equal(Utc(2026, 4, 29, 10, 30), loaded.LastEarnedAt);

        var row = Assert.Single(await context.GetAchievementRowsAsync());
        Assert.Equal("Completed", row.Status);
        Assert.Equal("2026-04-29T10:30:00.0000000", row.FinalizedAt);
        Assert.Equal(60, row.FrozenCurrentValue);
    }

    private static SqliteAchievementService CreateService(
        TestSqliteConnectionContext context,
        TempDirectory? trophies = null,
        DateTime? utcNow = null)
    {
        return new SqliteAchievementService(
            context,
            new FixedZoneTimeZoneService(TimeZoneInfo.Utc),
            new FixedClock(utcNow ?? Utc(2026, 4, 29, 10, 30)),
            trophies == null ? null : trophies.GetAchievementFolder);
    }

    private static DateTime Local(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
        public DateTime LocalNow => new(UtcNow.Ticks, DateTimeKind.Unspecified);
        public DateTimeOffset UtcNowOffset => new(UtcNow);
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"PointsAchievementServiceTests-{Guid.NewGuid():N}");

        public string GetAchievementFolder(int achievementId)
        {
            var folder = Path.Combine(_path, $"AchievementID_{achievementId}");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public void Dispose()
        {
            if (Directory.Exists(_path))
                Directory.Delete(_path, recursive: true);
        }
    }

    private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
    {
        private SQLiteAsyncConnection? _db;

        public TestSqliteConnectionContext()
        {
            DatabasePath = Path.Combine(
                Path.GetTempPath(),
                $"PointsAchievementServiceTests-{Guid.NewGuid():N}.db");
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
                CREATE TABLE IF NOT EXISTS AchievementCard (
                    AchievementCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Status TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    TargetType TEXT NOT NULL DEFAULT '',
                    DifficultyLevel TEXT NOT NULL DEFAULT '',
                    CreatedDate TEXT NOT NULL DEFAULT '',
                    LastEarnedAt TEXT NULL,
                    TargetActiveTimeInSeconds INTEGER NULL,
                    TargetValue REAL NULL,
                    ScCardStepID INTEGER NULL,
                    CompletionType TEXT NOT NULL DEFAULT '',
                    RangeUnit TEXT NULL,
                    RangeAmount INTEGER NULL,
                    DeadlineStart TEXT NULL,
                    Deadline TEXT NULL,
                    FinalizedAt TEXT NULL,
                    FrozenCurrentValue REAL NULL,
                    TrophyURLs TEXT NOT NULL DEFAULT '',
                    IsPinned INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS AchievementTrophy (
                    TrophyID INTEGER PRIMARY KEY AUTOINCREMENT,
                    AchievementCardID INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    EarnedOn TEXT NOT NULL,
                    ImageSource TEXT NOT NULL
                );
                """);
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
                CREATE TABLE IF NOT EXISTS ScCard (
                    ScCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ScCardStep (
                    ScCardStepID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ScCardID INTEGER NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ScCardStepRep (
                    ScCardStepID INTEGER NOT NULL,
                    TimeStamp TEXT NOT NULL,
                    StepValue REAL NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS MissionCard (
                    MissionCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    CompletedDate TEXT NULL,
                    Value REAL NOT NULL DEFAULT 0
                );
                """);
        }

        public async Task<long> InsertCardAsync(string title, string tags)
        {
            await InitializeAsync();
            await Db.ExecuteAsync("INSERT INTO Card (Title, Tags) VALUES (?, ?);", title, tags);
            return await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        public async Task InsertActivityAsync(long cardId, DateTime startUtc, DateTime endUtc, double valuePerMinute)
        {
            await InitializeAsync();
            await Db.ExecuteAsync(
                @"INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                  VALUES (?, ?, ?, ?, ?);",
                cardId,
                StrictTimeSerializer.SerializeUtcInstant(startUtc),
                StrictTimeSerializer.SerializeUtcInstant(endUtc),
                "Base",
                valuePerMinute);
        }

        public async Task<List<AchievementRow>> GetAchievementRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<AchievementRow>(
                @"SELECT AchievementCardID, CardID, Status, Description, TargetType, DifficultyLevel,
                         CreatedDate, LastEarnedAt, TargetActiveTimeInSeconds, TargetValue, CompletionType,
                         RangeUnit, RangeAmount, DeadlineStart, Deadline, FinalizedAt, FrozenCurrentValue,
                         TrophyURLs, IsPinned
                  FROM AchievementCard
                  ORDER BY AchievementCardID;");
        }

        public async Task<List<TrophyRow>> GetTrophyRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<TrophyRow>(
                @"SELECT TrophyID, AchievementCardID, Title, EarnedOn, ImageSource
                  FROM AchievementTrophy
                  ORDER BY TrophyID;");
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

    public sealed class AchievementRow
    {
        public long AchievementCardID { get; set; }
        public long CardID { get; set; }
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public string TargetType { get; set; } = "";
        public string DifficultyLevel { get; set; } = "";
        public string CreatedDate { get; set; } = "";
        public string? LastEarnedAt { get; set; }
        public int? TargetActiveTimeInSeconds { get; set; }
        public double? TargetValue { get; set; }
        public string CompletionType { get; set; } = "";
        public string? RangeUnit { get; set; }
        public int? RangeAmount { get; set; }
        public string? DeadlineStart { get; set; }
        public string? Deadline { get; set; }
        public string? FinalizedAt { get; set; }
        public double? FrozenCurrentValue { get; set; }
        public string TrophyURLs { get; set; } = "";
        public int IsPinned { get; set; }
    }

    public sealed class TrophyRow
    {
        public long TrophyID { get; set; }
        public long AchievementCardID { get; set; }
        public string Title { get; set; } = "";
        public string EarnedOn { get; set; } = "";
        public string ImageSource { get; set; } = "";
    }
}
