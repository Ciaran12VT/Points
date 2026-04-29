using Points.Models;
using Points.Services.Locks;
using Points.Services.Sqlite.Interfaces;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Locks
{
    public sealed class SqliteLockServiceTests
    {
        [Fact]
        public async Task SaveLocksForCardAsync_InsertsAndReadsLocksWithSchedulesAndDependencies()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteLockService(context);

            var from = new DateTime(2026, 4, 29, 9, 30, 0, DateTimeKind.Unspecified);
            var to = new DateTime(2026, 4, 29, 17, 45, 0, DateTimeKind.Unspecified);

            await service.SaveLocksForCardAsync(101, new List<LockModel>
            {
                new()
                {
                    LockNumber = 1,
                    TimeWindowStart = new TimeOnly(9, 0),
                    TimeWindowEnd = new TimeOnly(12, 0),
                    Schedules = new List<LockScheduleModel>
                    {
                        new()
                        {
                            FrequencyType = FrequencyType.EveryMonday,
                            FrequencyValue = 0,
                            FromDateTime = from,
                            ToDateTime = to
                        }
                    },
                    Dependencies = new List<LockTaskDependencyModel>
                    {
                        new()
                        {
                            TaskDependencyCardId = 202,
                            MetricType = LockDependencyMetricType.Points,
                            TimeScope = TimeScope.Weekly,
                            TargetValue = 12.5,
                            TargetValence = TargetValence.MustBeLessThan
                        }
                    }
                }
            });

            var saved = Assert.Single(await service.GetLocksForCardAsync(101));
            Assert.True(saved.LockId > 0);
            Assert.Equal(101, saved.CardId);
            Assert.Equal(1, saved.LockNumber);
            Assert.Equal(new TimeOnly(9, 0), saved.TimeWindowStart);
            Assert.Equal(new TimeOnly(12, 0), saved.TimeWindowEnd);

            var schedule = Assert.Single(saved.Schedules);
            Assert.True(schedule.ScheduleId > 0);
            Assert.Equal(saved.LockId, schedule.LockId);
            Assert.Equal(FrequencyType.EveryMonday, schedule.FrequencyType);
            Assert.Equal(from, schedule.FromDateTime);
            Assert.Equal(to, schedule.ToDateTime);

            var dependency = Assert.Single(saved.Dependencies);
            Assert.True(dependency.LockTaskDependencyId > 0);
            Assert.Equal(saved.LockId, dependency.LockId);
            Assert.Equal(202, dependency.TaskDependencyCardId);
            Assert.Equal(LockDependencyMetricType.Points, dependency.MetricType);
            Assert.Equal(TimeScope.Weekly, dependency.TimeScope);
            Assert.Equal(12.5, dependency.TargetValue);
            Assert.Equal(TargetValence.MustBeLessThan, dependency.TargetValence);
        }

        [Fact]
        public async Task SaveLocksForCardAsync_ReplacesLocksForCardOnly()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteLockService(context);

            await service.SaveLocksForCardAsync(101, new List<LockModel>
            {
                NewLock(lockNumber: 1, startHour: 8, taskDependencyCardId: 201)
            });

            await service.SaveLocksForCardAsync(202, new List<LockModel>
            {
                NewLock(lockNumber: 1, startHour: 10, taskDependencyCardId: 301)
            });

            await service.SaveLocksForCardAsync(101, new List<LockModel>
            {
                NewLock(lockNumber: 2, startHour: 13, taskDependencyCardId: 401)
            });

            var card101Lock = Assert.Single(await service.GetLocksForCardAsync(101));
            Assert.Equal(2, card101Lock.LockNumber);
            Assert.Equal(new TimeOnly(13, 0), card101Lock.TimeWindowStart);
            Assert.Equal(401, Assert.Single(card101Lock.Dependencies).TaskDependencyCardId);

            var card202Lock = Assert.Single(await service.GetLocksForCardAsync(202));
            Assert.Equal(1, card202Lock.LockNumber);
            Assert.Equal(new TimeOnly(10, 0), card202Lock.TimeWindowStart);
            Assert.Equal(301, Assert.Single(card202Lock.Dependencies).TaskDependencyCardId);
        }

        [Fact]
        public async Task DeleteLockModelAsync_RemovesLockAndChildren()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteLockService(context);

            await service.SaveLocksForCardAsync(101, new List<LockModel>
            {
                NewLock(lockNumber: 1, startHour: 8, taskDependencyCardId: 201)
            });

            var saved = Assert.Single(await service.GetLocksForCardAsync(101));

            await service.DeleteLockModelAsync(saved);

            Assert.Empty(await service.GetLocksForCardAsync(101));
            Assert.Equal(0, await context.CountAsync("Lock"));
            Assert.Equal(0, await context.CountAsync("LockSchedule"));
            Assert.Equal(0, await context.CountAsync("LockTaskDependency"));
        }

        [Fact]
        public async Task DeleteLockModelAsync_UsesCardAndLockNumberWhenIdIsMissing()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteLockService(context);

            await service.SaveLocksForCardAsync(101, new List<LockModel>
            {
                NewLock(lockNumber: 3, startHour: 8, taskDependencyCardId: 201)
            });

            await service.DeleteLockModelAsync(new LockModel
            {
                CardId = 101,
                LockNumber = 3
            });

            Assert.Empty(await service.GetLocksForCardAsync(101));
        }

        private static LockModel NewLock(int lockNumber, int startHour, long taskDependencyCardId)
        {
            return new LockModel
            {
                LockNumber = lockNumber,
                TimeWindowStart = new TimeOnly(startHour, 0),
                TimeWindowEnd = new TimeOnly(startHour + 1, 0),
                Schedules = new List<LockScheduleModel>
                {
                    new()
                    {
                        FrequencyType = FrequencyType.Once,
                        FrequencyValue = 0,
                        FromDateTime = new DateTime(2026, 4, 29, startHour, 0, 0, DateTimeKind.Unspecified),
                        ToDateTime = null
                    }
                },
                Dependencies = new List<LockTaskDependencyModel>
                {
                    new()
                    {
                        TaskDependencyCardId = taskDependencyCardId,
                        MetricType = LockDependencyMetricType.ActiveTime,
                        TimeScope = TimeScope.Daily,
                        TargetValue = 1,
                        TargetValence = TargetValence.MustBeGreaterThan
                    }
                }
            };
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsLockServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS Lock (
                        LockId INTEGER PRIMARY KEY AUTOINCREMENT,
                        LockNumber INTEGER NOT NULL,
                        CardId INTEGER NOT NULL,
                        TimeWindowStart TEXT NOT NULL,
                        TimeWindowEnd TEXT NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS LockSchedule (
                        ScheduleId INTEGER PRIMARY KEY AUTOINCREMENT,
                        LockId INTEGER NOT NULL,
                        FrequencyType TEXT NOT NULL,
                        FrequencyValue INTEGER NOT NULL DEFAULT 0,
                        FromDateTime TEXT NOT NULL,
                        ToDateTime TEXT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS LockTaskDependency (
                        LockTaskDependencyId INTEGER PRIMARY KEY AUTOINCREMENT,
                        LockId INTEGER NOT NULL,
                        TaskDependencyCardId INTEGER NOT NULL,
                        MetricType INTEGER NOT NULL DEFAULT 0,
                        TimeScope INTEGER NOT NULL DEFAULT 0,
                        TargetValue REAL NOT NULL DEFAULT 0,
                        TargetValence INTEGER NOT NULL DEFAULT 0
                    );
                    """);
            }

            public async Task<int> CountAsync(string tableName)
            {
                await InitializeAsync();
                return await Db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {tableName};");
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
