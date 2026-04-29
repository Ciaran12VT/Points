using Points.Models;
using Points.Services.Goals;
using Points.Services.Sqlite.Interfaces;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Goals
{
    public sealed class SqliteGoalServiceTests
    {
        [Fact]
        public async Task SaveGoalModelsDataAsync_InsertsAndReadsGoals()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteGoalService(context);
            await context.InsertCardAsync(101);

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
            {
                new()
                {
                    CardId = 101,
                    TimeScope = TimeScope.Daily,
                    GoalHrs = 2.5,
                    Enabled = true,
                    DeFactoStart = new TimeOnly(9, 30),
                    DeFactoEnd = new TimeOnly(17, 15)
                }
            });

            var saved = Assert.Single(await service.GetGoalModelsDataAsync());
            Assert.Equal(101, saved.CardId);
            Assert.Equal(TimeScope.Daily, saved.TimeScope);
            Assert.Equal(2.5, saved.GoalHrs);
            Assert.True(saved.Enabled);
            Assert.Equal(new TimeOnly(9, 30), saved.DeFactoStart);
            Assert.Equal(new TimeOnly(17, 15), saved.DeFactoEnd);
        }

        [Fact]
        public async Task SaveGoalModelsDataAsync_UpdatesExistingGoal()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteGoalService(context);
            await context.InsertCardAsync(101);

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
            {
                new() { CardId = 101, TimeScope = TimeScope.Weekly, GoalHrs = 5, Enabled = true }
            });

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
            {
                new() { CardId = 101, TimeScope = TimeScope.Weekly, GoalHrs = 8, Enabled = false }
            });

            var saved = Assert.Single(await service.GetGoalModelsDataAsync());
            Assert.Equal(8, saved.GoalHrs);
            Assert.False(saved.Enabled);
        }

        [Fact]
        public async Task SaveGoalModelsDataAsync_MirrorsOnlyIncomingScope()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteGoalService(context);
            await context.InsertCardAsync(101);
            await context.InsertCardAsync(202);

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
            {
                new() { CardId = 101, TimeScope = TimeScope.Daily, GoalHrs = 1, Enabled = true },
                new() { CardId = 202, TimeScope = TimeScope.Daily, GoalHrs = 2, Enabled = true }
            });

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
            {
                new() { CardId = 101, TimeScope = TimeScope.Weekly, GoalHrs = 3, Enabled = true }
            });

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
            {
                new() { CardId = 202, TimeScope = TimeScope.Daily, GoalHrs = 4, Enabled = true }
            });

            var saved = await service.GetGoalModelsDataAsync();

            Assert.DoesNotContain(saved, x => x.CardId == 101 && x.TimeScope == TimeScope.Daily);
            Assert.Contains(saved, x => x.CardId == 202 && x.TimeScope == TimeScope.Daily && x.GoalHrs == 4);
            Assert.Contains(saved, x => x.CardId == 101 && x.TimeScope == TimeScope.Weekly && x.GoalHrs == 3);
        }

        [Fact]
        public async Task SaveGoalModelsDataAsync_RejectsMixedScopes()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteGoalService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
                {
                    new() { CardId = 101, TimeScope = TimeScope.Daily, GoalHrs = 1 },
                    new() { CardId = 202, TimeScope = TimeScope.Weekly, GoalHrs = 2 }
                }));
        }

        [Fact]
        public async Task SaveGoalModelsDataAsync_EmptyListLeavesExistingGoals()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteGoalService(context);
            await context.InsertCardAsync(101);

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>
            {
                new() { CardId = 101, TimeScope = TimeScope.Monthly, GoalHrs = 10, Enabled = true }
            });

            await service.SaveGoalModelsDataAsync(new List<GoalDetailsModel>());

            Assert.Single(await service.GetGoalModelsDataAsync());
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsGoalServiceTests-{Guid.NewGuid():N}.db");
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
                        CardID INTEGER PRIMARY KEY
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS Goal (
                        GoalID INTEGER PRIMARY KEY,
                        CardID INTEGER NOT NULL,
                        TimeScope TEXT NOT NULL,
                        GoalHrs REAL NOT NULL,
                        Enabled INTEGER NOT NULL DEFAULT 0,
                        DeFactoStart TEXT NULL,
                        DeFactoEnd TEXT NULL,
                        FOREIGN KEY (CardID) REFERENCES Card(CardID) ON DELETE CASCADE,
                        UNIQUE (CardID, TimeScope)
                    );
                    """);
            }

            public async Task InsertCardAsync(long cardId)
            {
                await InitializeAsync();
                await Db.ExecuteAsync("INSERT OR IGNORE INTO Card (CardID) VALUES (?);", cardId);
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
