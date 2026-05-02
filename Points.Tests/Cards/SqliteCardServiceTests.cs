using Points.Services.Sqlite;
using Points.Evaluators;
using Points.Global;
using Points.Models;
using Points.Services.Cards;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Tests.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Cards;

public sealed class SqliteCardServiceTests
{
    [Fact]
    public async Task SaveCardModelAsync_InsertsUpdatesBaseCardAndDelegatesSubtypeSave()
    {
        await using var context = new TestSqliteConnectionContext();
        var harness = CreateHarness(context);
        var sc = new ScCardModel
        {
            Title = "Training",
            Tags = "health",
            Status = "In-Progress",
            Description = "Daily reps"
        };

        await harness.Service.SaveCardModelAsync(sc);

        Assert.True(sc.CardID > 0);
        Assert.True(sc.Id > 0);
        Assert.Equal(new[] { sc.CardID }, harness.Sc.SavedCardIds);

        var inserted = Assert.Single(await context.GetCardRowsAsync());
        Assert.Equal("Training", inserted.Title);
        Assert.Equal("health", inserted.Tags);

        sc.Title = "Training updated";
        sc.Tags = "health, focus";

        await harness.Service.SaveCardModelAsync(sc);

        var updated = Assert.Single(await context.GetCardRowsAsync());
        Assert.Equal(sc.CardID, updated.CardID);
        Assert.Equal("Training updated", updated.Title);
        Assert.Equal("health, focus", updated.Tags);
        Assert.Equal(new[] { sc.CardID, sc.CardID }, harness.Sc.SavedCardIds);
    }

    [Fact]
    public async Task SaveCardDisplayOrderAsync_PersistsSequentialOrder()
    {
        await using var context = new TestSqliteConnectionContext();
        var harness = CreateHarness(context);
        var first = new ScCardModel { Title = "First", Tags = "order" };
        var second = new ScCardModel { Title = "Second", Tags = "order" };
        var third = new ScCardModel { Title = "Third", Tags = "order" };

        await harness.Service.SaveCardModelAsync(first);
        await harness.Service.SaveCardModelAsync(second);
        await harness.Service.SaveCardModelAsync(third);

        await harness.Service.SaveCardDisplayOrderAsync(new ICardModel[] { third, first, second });

        var ordered = await context.GetCardRowsByDisplayOrderAsync();
        Assert.Equal(new[] { third.CardID, first.CardID, second.CardID }, ordered.Select(x => x.CardID));
        Assert.Equal(new[] { 0, 1, 2 }, ordered.Select(x => x.DisplayOrder));
        Assert.Equal(new[] { 0, 1, 2 }, new[] { third.DisplayOrder, first.DisplayOrder, second.DisplayOrder });
    }

    [Fact]
    public async Task DeleteCardModelAsync_RemovesBaseCardReferencesAndClearsModelIdentity()
    {
        await using var context = new TestSqliteConnectionContext();
        var harness = CreateHarness(context);
        var sc = new ScCardModel
        {
            Title = "Delete me",
            Tags = "cleanup"
        };
        await harness.Service.SaveCardModelAsync(sc);
        await context.InsertDeleteReferencesAsync(sc.CardID);

        var imageFolder = AppPaths.GetImageMetadataPath(sc.CardID);
        File.WriteAllText(Path.Combine(imageFolder, "field.jpg"), "test");

        Assert.False(await harness.Service.WouldArchiveCardModelOnDeleteAsync(sc));

        await harness.Service.DeleteCardModelAsync(sc);

        Assert.Equal(0, sc.Id);
        Assert.Equal(0, sc.CardID);
        Assert.Equal(0, await context.CountAsync("Card"));
        Assert.Equal(0, await context.CountAsync("ScCard"));
        Assert.Equal(0, await context.CountAsync("Shortcut"));
        Assert.Equal(0, await context.CountAsync("NotificationLog"));
        Assert.Equal(0, await context.CountAsync("CardSchedule"));
        Assert.Equal(0, await context.CountAsync("Lock"));
        Assert.Equal(0, await context.CountAsync("LockSchedule"));
        Assert.Equal(0, await context.CountAsync("LockTaskDependency"));
        Assert.False(Directory.Exists(imageFolder));
    }

    [Theory]
    [InlineData("Tat")]
    [InlineData("Sc")]
    [InlineData("Budget")]
    [InlineData("ValueTracker")]
    [InlineData("EventTracker")]
    public async Task DeleteCardModelAsync_ArchivesCardsWithTransactionalData(string kind)
    {
        await using var context = new TestSqliteConnectionContext();
        var harness = CreateHarness(context);
        var model = await context.InsertCardWithTransactionalDataAsync(kind);

        Assert.True(await harness.Service.WouldArchiveCardModelOnDeleteAsync(model));

        await harness.Service.DeleteCardModelAsync(model);

        Assert.Equal(1, await context.CountAsync("Card"));
        Assert.True(model.CardID > 0);
        Assert.True(model.Id > 0);
        Assert.Equal("Archived", await context.GetSubtypeStatusAsync(model));

        switch (model)
        {
            case TatCardModel tat:
                Assert.Equal("Archived", tat.Status);
                break;
            case BudgetCardModel budget:
                Assert.Equal("Archived", budget.Status);
                break;
            case TrackerCardModel tracker:
                Assert.Equal("Archived", tracker.Status);
                break;
        }
    }

    [Fact]
    public async Task GetHomeSeedDataAsync_ComposesBucketsFiltersCompletedMissionsAndPopulatesLocks()
    {
        await using var context = new TestSqliteConnectionContext();
        var harness = CreateHarness(context);
        var rangeStart = Utc(2026, 4, 29, 0);
        var rangeEnd = Utc(2026, 4, 29, 23, 59);
        var tat = new TatCardModel { CardID = 1, Title = "Focus", Tags = "work", IsLocksEnabled = true };
        var sc = new ScCardModel { CardID = 2, Title = "Practice", Tags = "music", IsLocksEnabled = true };
        var openMission = new MissionCardModel { CardID = 3, Title = "Open", Tags = "work" };
        var completedInRange = new MissionCardModel { CardID = 4, Title = "Done", Tags = "work" };
        completedInRange.Complete(Utc(2026, 4, 29, 12));
        var completedOutsideRange = new MissionCardModel { CardID = 5, Title = "Old", Tags = "work" };
        completedOutsideRange.Complete(Utc(2026, 4, 28, 12));

        harness.Tat.Models.Add(tat);
        harness.Tat.Models.Add(new TatCardModel { CardID = 10, Title = "Archived Tat", Status = "Archived" });
        harness.Sc.Models.Add(sc);
        harness.Sc.Models.Add(new ScCardModel { CardID = 11, Title = "Archived Sc", Status = "Archived" });
        harness.Mission.Models.AddRange(new[] { openMission, completedInRange, completedOutsideRange });
        harness.Budget.Models.Add(new BudgetCardModel { CardID = 6, Title = "Budget", Tags = "money" });
        harness.Budget.Models.Add(new BudgetCardModel { CardID = 12, Title = "Archived Budget", Status = "Archived" });
        harness.Achievement.Models.Add(new AchievementCardModel { CardID = 7, Title = "Achievement", Tags = "work" });
        harness.Tracker.ValueTrackers.Add(new ValueTrackerCardModel { CardID = 8, Title = "Weight", Tags = "health" });
        harness.Tracker.ValueTrackers.Add(new ValueTrackerCardModel { CardID = 13, Title = "Archived Value", Status = "Archived" });
        harness.Tracker.EventTrackers.Add(new EventTrackerCardModel { CardID = 9, Title = "Headache", Tags = "health" });
        harness.Tracker.EventTrackers.Add(new EventTrackerCardModel { CardID = 14, Title = "Archived Event", Status = "Archived" });

        var seed = await harness.Service.GetHomeSeedDataAsync(rangeStart, rangeEnd);

        Assert.Equal(new long[] { 1, 2 }, seed.MainQuestCards.Select(c => c.CardID));
        Assert.Equal(new long[] { 3, 4 }, seed.MissionCards.Select(c => c.CardID));
        Assert.Single(seed.BudgetCards);
        Assert.Single(seed.Achievements);
        Assert.Single(seed.ValueTrackers);
        Assert.Single(seed.EventTrackers);
        Assert.Equal(1, harness.Achievement.PopulateCalls);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, harness.Lock.RequestedCardIds);
        Assert.Single(tat.Locks);
        Assert.Single(sc.Locks);
        Assert.Single(openMission.Locks);
        Assert.Single(completedInRange.Locks);
        Assert.Empty(completedOutsideRange.Locks);
    }

    private static ServiceHarness CreateHarness(TestSqliteConnectionContext context)
    {
        var tat = new FakeTatCardService();
        var sc = new FakeScCardService(context);
        var mission = new FakeMissionCardService();
        var budget = new FakeBudgetService();
        var achievement = new FakeAchievementService();
        var tracker = new FakeTrackerService();
        var locks = new FakeLockService();
        var service = new SqliteCardService(
            context,
            new FixedZoneTimeZoneService(TimeZoneInfo.Utc),
            tat,
            sc,
            mission,
            budget,
            achievement,
            tracker,
            locks);

        return new ServiceHarness(service, tat, sc, mission, budget, achievement, tracker, locks);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }

    private static DateTime Local(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private sealed record ServiceHarness(
        SqliteCardService Service,
        FakeTatCardService Tat,
        FakeScCardService Sc,
        FakeMissionCardService Mission,
        FakeBudgetService Budget,
        FakeAchievementService Achievement,
        FakeTrackerService Tracker,
        FakeLockService Lock);

    private sealed class FakeTatCardService : ITatCardService
    {
        public List<TatCardModel> Models { get; } = new();

        public Task<TatCardModel> GetTatModelDataAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<List<TatCardModel>> GetTatModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            return Task.FromResult(Models.ToList());
        }

        public Task SaveTatModelDataAsync(TatCardModel model, long cardId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeScCardService : IScCardService
    {
        private readonly TestSqliteConnectionContext _context;

        public FakeScCardService(TestSqliteConnectionContext context)
        {
            _context = context;
        }

        public List<ScCardModel> Models { get; } = new();
        public List<long> SavedCardIds { get; } = new();

        public Task<ScCardModel> GetScModelDataAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<List<ScCardModel>> GetScModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            return Task.FromResult(Models.ToList());
        }

        public async Task SaveScModelDataAsync(ScCardModel model, long cardId)
        {
            SavedCardIds.Add(cardId);
            await _context.InitializeAsync();

            if (model.Id == 0)
            {
                await _context.Db.ExecuteAsync(
                    "INSERT INTO ScCard (CardID, Status, Description) VALUES (?, ?, ?);",
                    cardId,
                    model.Status,
                    model.Description);
                model.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                return;
            }

            await _context.Db.ExecuteAsync(
                "UPDATE ScCard SET CardID = ?, Status = ?, Description = ? WHERE ScCardID = ?;",
                cardId,
                model.Status,
                model.Description,
                model.Id);
        }

        public Task RemoveRepForStepAsync(int scCardStepId, DateTime repTime)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMissionCardService : IMissionCardService
    {
        public List<MissionCardModel> Models { get; } = new();

        public Task<MissionCardModel> GetMissionCardModelDataAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null)
        {
            return Task.FromResult(Models.ToList());
        }

        public Task SaveMissionCardModelDataAsync(MissionCardModel model, long cardId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeBudgetService : IBudgetService
    {
        public List<BudgetCardModel> Models { get; } = new();

        public Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string? whereClause = null)
        {
            return Task.FromResult(Models.ToList());
        }

        public Task SaveBudgetCardModelDataAsync(BudgetCardModel model, long cardId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeAchievementService : IAchievementService
    {
        public List<AchievementCardModel> Models { get; } = new();
        public int PopulateCalls { get; private set; }

        public Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync()
        {
            return Task.FromResult(Models.ToList());
        }

        public Task<List<TrophyModel>> GetTrophyModelsDataAsync()
        {
            return Task.FromResult(new List<TrophyModel>());
        }

        public Task SaveAchievementCardModelDataAsync(AchievementCardModel acm, long cardId)
        {
            throw new NotSupportedException();
        }

        public Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAchievementCardModelAsync(AchievementCardModel model)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAchievementTrophyAsync(int trophyId)
        {
            throw new NotSupportedException();
        }

        public Task PopulateAchievementsAsync(
            List<AchievementCardModel> achievements,
            List<IActiveCardModel> mainQuest,
            List<MissionCardModel> mission)
        {
            PopulateCalls++;
            return Task.CompletedTask;
        }

        public Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(
            List<TimeValueAchievementEvaluator> timeValueAchievementEvaluators)
        {
            return Task.FromResult(timeValueAchievementEvaluators);
        }

        public Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card)
        {
            return Task.FromResult(card);
        }
    }

    private sealed class FakeTrackerService : ITrackerService
    {
        public List<ValueTrackerCardModel> ValueTrackers { get; } = new();
        public List<EventTrackerCardModel> EventTrackers { get; } = new();

        public Task<ValueTrackerCardModel> GetValueTrackerCardModelDataAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<List<ValueTrackerCardModel>> GetValueTrackerCardModelsDataAsync(string? whereClause = null)
        {
            return Task.FromResult(ValueTrackers.ToList());
        }

        public Task<EventTrackerCardModel> GetEventTrackerCardModelDataAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<List<EventTrackerCardModel>> GetEventTrackerCardModelsDataAsync(string? whereClause = null)
        {
            return Task.FromResult(EventTrackers.ToList());
        }

        public Task SaveValueTrackerCardModelDataAsync(ValueTrackerCardModel model, long cardId)
        {
            throw new NotSupportedException();
        }

        public Task SaveEventTrackerCardModelDataAsync(EventTrackerCardModel model, long cardId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeLockService : ILockService
    {
        public List<long> RequestedCardIds { get; } = new();

        public Task<List<LockModel>> GetLocksForCardAsync(long cardId)
        {
            RequestedCardIds.Add(cardId);
            return Task.FromResult(new List<LockModel>
            {
                new()
                {
                    CardId = cardId,
                    LockNumber = (int)cardId,
                    TimeWindowStart = new TimeOnly(9, 0),
                    TimeWindowEnd = new TimeOnly(17, 0)
                }
            });
        }

        public Task SaveLocksForCardAsync(long cardId, List<LockModel> locksToSave)
        {
            throw new NotSupportedException();
        }

        public Task DeleteLockModelAsync(LockModel lockModel)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
    {
        private SQLiteAsyncConnection? _db;

        public TestSqliteConnectionContext()
        {
            DatabasePath = Path.Combine(
                Path.GetTempPath(),
                $"PointsCardServiceTests-{Guid.NewGuid():N}.db");
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
                    ScCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Status TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
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
                CREATE TABLE IF NOT EXISTS Activity (
                    ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Start TEXT NOT NULL,
                    "End" TEXT NULL,
                    ValueRateName TEXT NOT NULL DEFAULT '',
                    ValuePerMinute REAL NOT NULL,
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ScCardStep (
                    ScCardStepID INTEGER PRIMARY KEY AUTOINCREMENT,
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
                CREATE TABLE IF NOT EXISTS BudgetCard (
                    BudgetCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Status TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    Currency TEXT NOT NULL DEFAULT '',
                    ExchangeRate REAL NOT NULL,
                    StartDate TEXT NOT NULL,
                    InitialBalance REAL NOT NULL,
                    FOREIGN KEY(CardID) REFERENCES Card(CardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS BudgetCardTransaction (
                    BudgetCardTransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    BudgetCardID INTEGER NOT NULL,
                    Amount REAL NOT NULL,
                    Type TEXT NOT NULL DEFAULT '',
                    TimeStamp TEXT NOT NULL,
                    FOREIGN KEY(BudgetCardID) REFERENCES BudgetCard(BudgetCardID) ON DELETE CASCADE
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS ValueTrackerCard (
                    ValueTrackerCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID INTEGER NOT NULL,
                    Status TEXT NOT NULL DEFAULT '',
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
                    Status TEXT NOT NULL DEFAULT '',
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
                CREATE TABLE IF NOT EXISTS Shortcut (
                    ShortcutId INTEGER PRIMARY KEY AUTOINCREMENT,
                    TargetCardId INTEGER NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS NotificationLog (
                    NotificationLogId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardId INTEGER NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS CardSchedule (
                    ScheduleID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardId INTEGER NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS Lock (
                    LockId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardId INTEGER NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS LockSchedule (
                    LockScheduleId INTEGER PRIMARY KEY AUTOINCREMENT,
                    LockId INTEGER NOT NULL
                );
                """);
            await _db.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS LockTaskDependency (
                    LockTaskDependencyId INTEGER PRIMARY KEY AUTOINCREMENT,
                    LockId INTEGER NOT NULL,
                    TaskDependencyCardId INTEGER NOT NULL
                );
                """);
        }

        public async Task<List<CardRow>> GetCardRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<CardRow>(
                @"SELECT CardID, DisplayOrder, Title, Tags
                  FROM Card
                  ORDER BY CardID;");
        }

        public async Task<List<CardRow>> GetCardRowsByDisplayOrderAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<CardRow>(
                @"SELECT CardID, DisplayOrder, Title, Tags
                  FROM Card
                  ORDER BY DisplayOrder, CardID;");
        }

        public async Task InsertDeleteReferencesAsync(long cardId)
        {
            await InitializeAsync();
            await Db.ExecuteAsync("INSERT INTO Shortcut (TargetCardId) VALUES (?);", cardId);
            await Db.ExecuteAsync("INSERT INTO NotificationLog (CardId) VALUES (?);", cardId);
            await Db.ExecuteAsync("INSERT INTO CardSchedule (CardId) VALUES (?);", cardId);
            await Db.ExecuteAsync("INSERT INTO Lock (CardId) VALUES (?);", cardId);
            var lockId = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            await Db.ExecuteAsync("INSERT INTO LockSchedule (LockId) VALUES (?);", lockId);
            await Db.ExecuteAsync("INSERT INTO LockTaskDependency (LockId, TaskDependencyCardId) VALUES (?, ?);", lockId, 999);
            await Db.ExecuteAsync("INSERT INTO LockTaskDependency (LockId, TaskDependencyCardId) VALUES (?, ?);", 999, cardId);
        }

        public async Task<ICardModel> InsertCardWithTransactionalDataAsync(string kind)
        {
            await InitializeAsync();

            var title = kind + " card";
            var cardId = await InsertCardAsync(title, "test");
            var timestamp = StrictTimeSerializer.SerializeUtcInstant(Utc(2026, 4, 29, 9));

            switch (kind)
            {
                case "Tat":
                    await Db.ExecuteAsync(
                        @"INSERT INTO TatCard (CardID, ValuePerMinute, Status, Description)
                          VALUES (?, ?, ?, ?);",
                        cardId,
                        1,
                        "In-Progress",
                        "");
                    var tatId = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    await Db.ExecuteAsync(
                        @"INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                          VALUES (?, ?, ?, ?, ?);",
                        cardId,
                        timestamp,
                        StrictTimeSerializer.SerializeUtcInstant(Utc(2026, 4, 29, 10)),
                        "Base",
                        1);
                    return new TatCardModel { Id = tatId, CardID = cardId, Status = "In-Progress" };

                case "Sc":
                    await Db.ExecuteAsync(
                        @"INSERT INTO ScCard (CardID, Status, Description)
                          VALUES (?, ?, ?);",
                        cardId,
                        "In-Progress",
                        "");
                    var scId = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    await Db.ExecuteAsync(
                        @"INSERT INTO ScCardStep (ScCardID, SortOrder, Title, StepValue)
                          VALUES (?, ?, ?, ?);",
                        scId,
                        1,
                        "Step",
                        1);
                    var stepId = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    await Db.ExecuteAsync(
                        @"INSERT INTO ScCardStepRep (ScCardStepID, TimeStamp, StepValue)
                          VALUES (?, ?, ?);",
                        stepId,
                        timestamp,
                        1);
                    return new ScCardModel { Id = scId, CardID = cardId, Status = "In-Progress" };

                case "Budget":
                    await Db.ExecuteAsync(
                        @"INSERT INTO BudgetCard
                            (CardID, Status, Description, Currency, ExchangeRate, StartDate, InitialBalance)
                          VALUES (?, ?, ?, ?, ?, ?, ?);",
                        cardId,
                        "In-Progress",
                        "",
                        "EUR",
                        1,
                        StrictTimeSerializer.SerializeLocalDateTime(Local(2026, 4, 1)),
                        0);
                    var budgetId = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    await Db.ExecuteAsync(
                        @"INSERT INTO BudgetCardTransaction (BudgetCardID, Amount, Type, TimeStamp)
                          VALUES (?, ?, ?, ?);",
                        budgetId,
                        10,
                        "Spend",
                        timestamp);
                    return new BudgetCardModel { Id = budgetId, CardID = cardId, Status = "In-Progress" };

                case "ValueTracker":
                    await Db.ExecuteAsync(
                        @"INSERT INTO ValueTrackerCard
                            (CardID, Status, Unit, CreatedDate, RangeStart, ScheduleEvery, ScheduleUnit)
                          VALUES (?, ?, ?, ?, ?, ?, ?);",
                        cardId,
                        "In-Progress",
                        "kg",
                        StrictTimeSerializer.SerializeLocalDateTime(Local(2026, 4, 1)),
                        StrictTimeSerializer.SerializeLocalDateTime(Local(2026, 4, 1)),
                        1,
                        "Week");
                    var valueTrackerId = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    await Db.ExecuteAsync(
                        @"INSERT INTO TrackerValue (CardID, TimeStamp, Value)
                          VALUES (?, ?, ?);",
                        cardId,
                        timestamp,
                        1);
                    return new ValueTrackerCardModel { Id = valueTrackerId, CardID = cardId, Status = "In-Progress" };

                case "EventTracker":
                    await Db.ExecuteAsync(
                        @"INSERT INTO EventTrackerCard
                            (CardID, Status, Unit, CreatedDate, RangeStart, GroupByPeriod)
                          VALUES (?, ?, ?, ?, ?, ?);",
                        cardId,
                        "In-Progress",
                        "event",
                        StrictTimeSerializer.SerializeLocalDateTime(Local(2026, 4, 1)),
                        StrictTimeSerializer.SerializeLocalDateTime(Local(2026, 4, 1)),
                        "Day");
                    var eventTrackerId = (int)await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    await Db.ExecuteAsync(
                        @"INSERT INTO TrackerValue (CardID, TimeStamp, Value)
                          VALUES (?, ?, ?);",
                        cardId,
                        timestamp,
                        1);
                    return new EventTrackerCardModel { Id = eventTrackerId, CardID = cardId, Status = "In-Progress" };

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public async Task<long> InsertCardAsync(string title, string tags)
        {
            await InitializeAsync();
            await Db.ExecuteAsync("INSERT INTO Card (Title, Tags) VALUES (?, ?);", title, tags);
            return await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        public async Task<string> GetSubtypeStatusAsync(ICardModel model)
        {
            await InitializeAsync();

            return model switch
            {
                ScCardModel => await Db.ExecuteScalarAsync<string>(
                    "SELECT Status FROM ScCard WHERE CardID = ?;",
                    model.CardID),
                TatCardModel => await Db.ExecuteScalarAsync<string>(
                    "SELECT Status FROM TatCard WHERE CardID = ?;",
                    model.CardID),
                BudgetCardModel => await Db.ExecuteScalarAsync<string>(
                    "SELECT Status FROM BudgetCard WHERE CardID = ?;",
                    model.CardID),
                ValueTrackerCardModel => await Db.ExecuteScalarAsync<string>(
                    "SELECT Status FROM ValueTrackerCard WHERE CardID = ?;",
                    model.CardID),
                EventTrackerCardModel => await Db.ExecuteScalarAsync<string>(
                    "SELECT Status FROM EventTrackerCard WHERE CardID = ?;",
                    model.CardID),
                _ => ""
            };
        }

        public async Task<int> CountAsync(string table)
        {
            await InitializeAsync();
            return await Db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table};");
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

    public sealed class CardRow
    {
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string Title { get; set; } = "";
        public string Tags { get; set; } = "";
    }
}
