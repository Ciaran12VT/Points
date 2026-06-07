using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Budgets;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Tests.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Budgets;

public sealed class SqliteBudgetServiceTests
{
    [Fact]
    public async Task SaveBudgetCardModelDataAsync_InsertsAndLoadsBudget()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Daily Calories", "health");
        var budget = new BudgetCardModel
        {
            Status = "In-Progress",
            Description = "Food budget",
            Currency = "Kcal",
            ExchangeRate = 0.02,
            StartDate = Local(2026, 4, 1, 9, 15),
            InitialBalance = 1200
        };
        budget.TopUps.Add(new ScheduledTopUp { Amount = 500, TimeOfDay = TimeSpan.FromHours(7) });
        budget.TopUps.Add(new ScheduledTopUp { Amount = 250, TimeOfDay = TimeSpan.FromHours(18) });
        budget.Transactions.Add(new BudgetTransaction
        {
            Timestamp = Utc(2026, 4, 29, 10, 30),
            Type = BudgetTransactionType.Spend,
            CurrencyAmount = 100
        });
        budget.Transactions.Add(new BudgetTransaction
        {
            Timestamp = Utc(2026, 4, 29, 12, 0),
            Type = BudgetTransactionType.CashIn,
            CurrencyAmount = 50
        });

        await service.SaveBudgetCardModelDataAsync(budget, cardId);

        Assert.True(budget.Id > 0);
        Assert.All(budget.TopUps, topup => Assert.True(topup.Id > 0));
        Assert.All(budget.Transactions, transaction => Assert.True(transaction.Id > 0));

        var row = Assert.Single(await context.GetBudgetRowsAsync());
        Assert.Equal(cardId, row.CardID);
        Assert.Equal("In-Progress", row.Status);
        Assert.Equal("Food budget", row.Description);
        Assert.Equal("Kcal", row.Currency);
        Assert.Equal(0.02, row.ExchangeRate);
        Assert.Equal("2026-04-01T09:15:00.0000000", row.StartDate);
        Assert.Equal(1200, row.InitialBalance);

        var loaded = await service.GetBudgetCardModelDataAsync(budget.Id);

        Assert.Equal("Daily Calories", loaded.Title);
        Assert.Equal("health", loaded.Tags);
        Assert.Equal(Local(2026, 4, 1, 9, 15), loaded.StartDate);
        Assert.Equal(new[] { 500d, 250d }, loaded.TopUps.Select(x => x.Amount));
        Assert.Equal(new[] { BudgetTransactionType.Spend, BudgetTransactionType.CashIn }, loaded.Transactions.Select(x => x.Type));
        Assert.Equal(Utc(2026, 4, 29, 10, 30), loaded.Transactions[0].Timestamp);
        Assert.Equal(1, loaded.Transactions[1].GlobalValueAmount);
    }

    [Fact]
    public async Task SaveBudgetCardModelDataAsync_UpdatesBudgetAndSyncsChildDeletes()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var cardId = await context.InsertCardAsync("Budget", "money");
        var budget = new BudgetCardModel
        {
            Status = "In-Progress",
            Description = "Original",
            Currency = "EUR",
            ExchangeRate = 2,
            StartDate = Local(2026, 4, 1),
            InitialBalance = 100
        };
        budget.TopUps.Add(new ScheduledTopUp { Amount = 10, TimeOfDay = TimeSpan.FromHours(8) });
        budget.TopUps.Add(new ScheduledTopUp { Amount = 20, TimeOfDay = TimeSpan.FromHours(20) });
        budget.Transactions.Add(new BudgetTransaction
        {
            Timestamp = Utc(2026, 4, 29, 9),
            Type = BudgetTransactionType.Spend,
            CurrencyAmount = 5
        });
        budget.Transactions.Add(new BudgetTransaction
        {
            Timestamp = Utc(2026, 4, 29, 10),
            Type = BudgetTransactionType.CashIn,
            CurrencyAmount = 6
        });
        await service.SaveBudgetCardModelDataAsync(budget, cardId);

        var removedTransactionId = budget.Transactions[0].Id;
        await context.InsertMetadataAsync(cardId, removedTransactionId);

        budget.Status = "Paused";
        budget.Description = "Updated";
        budget.ExchangeRate = 3;
        budget.InitialBalance = 150;

        budget.TopUps.RemoveAt(0);
        budget.TopUps[0].Amount = 25;
        budget.TopUps.Add(new ScheduledTopUp { Amount = 40, TimeOfDay = TimeSpan.FromHours(12) });

        budget.Transactions.RemoveAt(0);
        budget.Transactions[0].CurrencyAmount = 7;
        budget.Transactions.Add(new BudgetTransaction
        {
            Timestamp = Utc(2026, 4, 29, 11),
            Type = BudgetTransactionType.Spend,
            CurrencyAmount = 8
        });

        await service.SaveBudgetCardModelDataAsync(budget, cardId);

        var row = Assert.Single(await context.GetBudgetRowsAsync());
        Assert.Equal("Paused", row.Status);
        Assert.Equal("Updated", row.Description);
        Assert.Equal(3, row.ExchangeRate);
        Assert.Equal(150, row.InitialBalance);

        var topups = await context.GetTopUpRowsAsync(budget.Id);
        Assert.Equal(new[] { 25d, 40d }, topups.Select(x => x.Amount));

        var transactions = await context.GetTransactionRowsAsync(budget.Id);
        Assert.Equal(new[] { 7d, 8d }, transactions.Select(x => x.Amount));
        Assert.DoesNotContain(transactions, x => x.BudgetCardTransactionID == removedTransactionId);
        Assert.Empty(await context.GetMetadataRowsAsync(removedTransactionId));
    }

    [Fact]
    public async Task GetBudgetCardModelsDataAsync_LoadsOnlyMatchingBudgetsWithChildren()
    {
        await using var context = new TestSqliteConnectionContext();
        var service = CreateService(context);
        var firstCardId = await context.InsertCardAsync("Groceries", "food");
        var secondCardId = await context.InsertCardAsync("Archived", "old");

        var first = new BudgetCardModel
        {
            Status = "In-Progress",
            Currency = "EUR",
            ExchangeRate = 0.5,
            StartDate = Local(2026, 4, 1),
            InitialBalance = 100
        };
        first.TopUps.Add(new ScheduledTopUp { Amount = 5, TimeOfDay = TimeSpan.FromHours(8) });
        first.Transactions.Add(new BudgetTransaction
        {
            Timestamp = Utc(2026, 4, 29, 9),
            Type = BudgetTransactionType.CashIn,
            CurrencyAmount = 10
        });

        var second = new BudgetCardModel
        {
            Status = "Archived",
            Currency = "EUR",
            ExchangeRate = 1,
            StartDate = Local(2026, 4, 1),
            InitialBalance = 100
        };

        await service.SaveBudgetCardModelDataAsync(first, firstCardId);
        await service.SaveBudgetCardModelDataAsync(second, secondCardId);

        var loaded = await service.GetBudgetCardModelsDataAsync("b.Status = 'In-Progress'");

        var budget = Assert.Single(loaded);
        Assert.Equal("Groceries", budget.Title);
        Assert.Single(budget.TopUps);
        var transaction = Assert.Single(budget.Transactions);
        Assert.Equal(5, transaction.GlobalValueAmount);
    }

    private static SqliteBudgetService CreateService(TestSqliteConnectionContext context)
    {
        return new SqliteBudgetService(context, new FixedZoneTimeZoneService(TimeZoneInfo.Utc));
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
                $"PointsBudgetServiceTests-{Guid.NewGuid():N}.db");
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
                CREATE TABLE IF NOT EXISTS BudgetCardScheduledTopUp (
                    BudgetCardScheduledTopUpID INTEGER PRIMARY KEY AUTOINCREMENT,
                    BudgetCardID INTEGER NOT NULL,
                    Amount REAL NOT NULL,
                    TimeOfDaySeconds INTEGER NOT NULL,
                    FOREIGN KEY(BudgetCardID) REFERENCES BudgetCard(BudgetCardID) ON DELETE CASCADE
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

        public async Task InsertMetadataAsync(long cardId, long transactionId)
        {
            await InitializeAsync();
            await Db.ExecuteAsync(
                @"INSERT INTO UdmdTrans (CardID, UdmdConfigID, RelatedEntityType, RelatedEntityId, FieldValue)
                  VALUES (?, ?, ?, ?, ?);",
                cardId,
                1,
                UdmdRelatedEntityTypes.BudgetTransaction,
                transactionId,
                "note");
        }

        public async Task<List<BudgetRow>> GetBudgetRowsAsync()
        {
            await InitializeAsync();
            return await Db.QueryAsync<BudgetRow>(
                @"SELECT BudgetCardID, CardID, Status, Description, Currency, ExchangeRate, StartDate, InitialBalance
                  FROM BudgetCard
                  ORDER BY BudgetCardID;");
        }

        public async Task<List<TopUpRow>> GetTopUpRowsAsync(long budgetId)
        {
            await InitializeAsync();
            return await Db.QueryAsync<TopUpRow>(
                @"SELECT BudgetCardScheduledTopUpID, BudgetCardID, Amount, TimeOfDaySeconds
                  FROM BudgetCardScheduledTopUp
                  WHERE BudgetCardID = ?
                  ORDER BY BudgetCardScheduledTopUpID;",
                budgetId);
        }

        public async Task<List<TransactionRow>> GetTransactionRowsAsync(long budgetId)
        {
            await InitializeAsync();
            return await Db.QueryAsync<TransactionRow>(
                @"SELECT BudgetCardTransactionID, BudgetCardID, Amount, Type, TimeStamp
                  FROM BudgetCardTransaction
                  WHERE BudgetCardID = ?
                  ORDER BY BudgetCardTransactionID;",
                budgetId);
        }

        public async Task<List<UdmdTransRow>> GetMetadataRowsAsync(long transactionId)
        {
            await InitializeAsync();
            return await Db.QueryAsync<UdmdTransRow>(
                @"SELECT UdmdTransID, RelatedEntityId
                  FROM UdmdTrans
                  WHERE RelatedEntityType = ?
                    AND RelatedEntityId = ?;",
                UdmdRelatedEntityTypes.BudgetTransaction,
                transactionId);
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

    public sealed class BudgetRow
    {
        public long BudgetCardID { get; set; }
        public long CardID { get; set; }
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public string Currency { get; set; } = "";
        public double ExchangeRate { get; set; }
        public string StartDate { get; set; } = "";
        public double InitialBalance { get; set; }
    }

    public sealed class TopUpRow
    {
        public long BudgetCardScheduledTopUpID { get; set; }
        public long BudgetCardID { get; set; }
        public double Amount { get; set; }
        public double TimeOfDaySeconds { get; set; }
    }

    public sealed class TransactionRow
    {
        public long BudgetCardTransactionID { get; set; }
        public long BudgetCardID { get; set; }
        public double Amount { get; set; }
        public string Type { get; set; } = "";
        public string TimeStamp { get; set; } = "";
    }

    public sealed class UdmdTransRow
    {
        public long UdmdTransID { get; set; }
        public long RelatedEntityId { get; set; }
    }
}
