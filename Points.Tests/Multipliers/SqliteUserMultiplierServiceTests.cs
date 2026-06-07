using Points.Global;
using Points.Models;
using Points.Services.Multipliers;
using Points.Services.Sqlite;
using Points.Services.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Multipliers
{
    public sealed class SqliteUserMultiplierServiceTests
    {
        [Fact]
        public async Task SetActiveMultiplierAsync_OpensIntervalAndUpdatesRuntimeState()
        {
            MultiplierRuntimeState.Clear();
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteUserMultiplierService(context);

            var saved = await service.SaveMultiplierAsync(new UserMultiplierModel
            {
                Name = "Sickness",
                Code = "sik",
                Description = "Activate when feeling ill.",
                MultiplyBy = 1.1
            }, Utc(0));

            await service.SetActiveMultiplierAsync(saved.Id, Utc(1));

            var active = await service.GetActiveMultiplierAsync();
            var interval = Assert.Single(await context.GetIntervalsAsync());

            Assert.NotNull(active);
            Assert.Equal("SIK", active!.Code);
            Assert.Equal("SIK", MultiplierRuntimeState.ActiveCode);
            Assert.Equal(1.1, MultiplierRuntimeState.ActiveMultiplyBy, precision: 6);
            Assert.Equal(Utc(1), StrictTimeSerializer.ParseUtcInstant(interval.Start));
            Assert.Null(interval.End);
        }

        [Fact]
        public async Task SetActiveMultiplierAsync_ClosesPreviousOpenInterval()
        {
            MultiplierRuntimeState.Clear();
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteUserMultiplierService(context);

            var sickness = await service.SaveMultiplierAsync(new UserMultiplierModel
            {
                Name = "Sickness",
                Code = "SIK",
                MultiplyBy = 1.1
            }, Utc(0));

            var lowMood = await service.SaveMultiplierAsync(new UserMultiplierModel
            {
                Name = "Low Mood",
                Code = "LOW",
                MultiplyBy = 1.2
            }, Utc(0));

            await service.SetActiveMultiplierAsync(sickness.Id, Utc(1));
            await service.SetActiveMultiplierAsync(lowMood.Id, Utc(2));

            var intervals = await context.GetIntervalsAsync();
            Assert.Equal(2, intervals.Count);
            Assert.Equal(Utc(2), StrictTimeSerializer.ParseUtcInstant(intervals[0].End!));
            Assert.Null(intervals[1].End);
            Assert.Equal("LOW", intervals[1].Code);

            var multipliers = await service.GetMultipliersAsync();
            Assert.False(multipliers.Single(x => x.Id == sickness.Id).IsActive);
            Assert.True(multipliers.Single(x => x.Id == lowMood.Id).IsActive);
        }

        [Fact]
        public async Task SaveMultiplierAsync_WhenActive_RecordsNewSnapshotInterval()
        {
            MultiplierRuntimeState.Clear();
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteUserMultiplierService(context);

            var saved = await service.SaveMultiplierAsync(new UserMultiplierModel
            {
                Name = "Sickness",
                Code = "SIK",
                MultiplyBy = 1.1
            }, Utc(0));

            await service.SetActiveMultiplierAsync(saved.Id, Utc(1));

            saved.Code = "LOW";
            saved.Name = "Low Mood";
            saved.MultiplyBy = 0.9;
            saved.IsActive = true;
            await service.SaveMultiplierAsync(saved, Utc(2));

            var intervals = await context.GetIntervalsAsync();
            Assert.Equal(2, intervals.Count);
            Assert.Equal(Utc(2), StrictTimeSerializer.ParseUtcInstant(intervals[0].End!));
            Assert.Equal("LOW", intervals[1].Code);
            Assert.Equal(0.9, intervals[1].MultiplyBy, precision: 6);
            Assert.Equal("LOW", MultiplierRuntimeState.ActiveCode);
        }

        [Fact]
        public async Task SaveMultiplierAsync_RejectsCodesLongerThanThreeCharacters()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteUserMultiplierService(context);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveMultiplierAsync(new UserMultiplierModel
                {
                    Name = "Sickness",
                    Code = "SICK",
                    MultiplyBy = 1.1
                }, Utc(0)));

            Assert.Contains("3 characters or fewer", ex.Message);
        }

        private static DateTime Utc(int minute)
        {
            return new DateTime(2026, 1, 1, 10, minute, 0, DateTimeKind.Utc);
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsUserMultiplierServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS UserMultiplier (
                        UserMultiplierID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL DEFAULT '',
                        Code TEXT NOT NULL DEFAULT '',
                        Description TEXT NOT NULL DEFAULT '',
                        MultiplyBy REAL NOT NULL DEFAULT 1.0,
                        CreatedAtUtc TEXT NOT NULL,
                        UpdatedAtUtc TEXT NOT NULL,
                        CHECK (length(Code) <= 3),
                        CHECK (MultiplyBy > 0)
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS UserMultiplierActivationInterval (
                        UserMultiplierActivationIntervalID INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserMultiplierID INTEGER NULL,
                        Name TEXT NOT NULL DEFAULT '',
                        Code TEXT NOT NULL DEFAULT '',
                        Description TEXT NOT NULL DEFAULT '',
                        MultiplyBy REAL NOT NULL,
                        Start TEXT NOT NULL,
                        "End" TEXT NULL,
                        FOREIGN KEY (UserMultiplierID) REFERENCES UserMultiplier(UserMultiplierID) ON DELETE SET NULL,
                        CHECK (length(Code) <= 3),
                        CHECK (MultiplyBy > 0),
                        CHECK ("End" IS NULL OR Start <= "End")
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS UX_UserMultiplier_Code
                    ON UserMultiplier(Code COLLATE NOCASE);
                    """);
                await _db.ExecuteAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS UX_UserMultiplierActivation_OneOpen
                    ON UserMultiplierActivationInterval(1) WHERE "End" IS NULL;
                    """);
            }

            public async Task<List<UserMultiplierActivationIntervalRow>> GetIntervalsAsync()
            {
                await InitializeAsync();

                return await Db.QueryAsync<UserMultiplierActivationIntervalRow>(
                    @"SELECT UserMultiplierActivationIntervalID, UserMultiplierID, Name, Code, Description, MultiplyBy, Start, ""End""
                      FROM UserMultiplierActivationInterval
                      ORDER BY UserMultiplierActivationIntervalID;");
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

            public async ValueTask DisposeAsync()
            {
                await CloseDatabaseAsync();

                if (File.Exists(DatabasePath))
                    File.Delete(DatabasePath);
            }
        }

        public sealed class UserMultiplierActivationIntervalRow
        {
            public int UserMultiplierActivationIntervalID { get; set; }
            public int? UserMultiplierID { get; set; }
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
            public string Description { get; set; } = "";
            public double MultiplyBy { get; set; }
            public string Start { get; set; } = "";
            public string? End { get; set; }
        }
    }
}
