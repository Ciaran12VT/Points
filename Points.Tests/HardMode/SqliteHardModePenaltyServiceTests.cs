using Points.Global;
using Points.Models;
using Points.Models.DbModels;
using Points.Services.HardMode;
using Points.Services.Persistence;
using Points.Services.Sqlite;
using Points.Services.Time;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.HardMode
{
    public sealed class SqliteHardModePenaltyServiceTests
    {
        [Fact]
        public async Task ReconcileAsync_OpensPenaltyIntervalWhenHardModeIsEnabledAndDeadAir()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = CreateService(context);

            await service.ReconcileAsync(true, 0.6, hasActiveActivity: false, Utc(10));

            var row = Assert.Single(await context.GetRowsAsync());
            Assert.Equal(-0.6, row.ValuePerMinute);
            Assert.Equal(Utc(10), StrictTimeSerializer.ParseUtcInstant(row.Start));
            Assert.Null(row.End);
        }

        [Fact]
        public async Task ReconcileAsync_ClosesOpenPenaltyIntervalWhenActivityStarts()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = CreateService(context);

            await service.ReconcileAsync(true, -0.6, hasActiveActivity: false, Utc(10));
            await service.ReconcileAsync(true, -0.6, hasActiveActivity: true, Utc(10).AddMinutes(5));

            var row = Assert.Single(await context.GetRowsAsync());
            Assert.Equal(Utc(10).AddMinutes(5), StrictTimeSerializer.ParseUtcInstant(row.End!));

            var value = await service.GetValueAsync(
                Utc(10),
                Utc(10).AddMinutes(10),
                Utc(10).AddMinutes(10));

            Assert.Equal(-3.0, value, precision: 6);
        }

        [Fact]
        public async Task GetValueAsync_OpenPenaltyIntervalUsesCurrentUtcNow()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = CreateService(context);

            await service.ReconcileAsync(true, -1.0, hasActiveActivity: false, Utc(10));

            var value = await service.GetValueAsync(
                Utc(10),
                Utc(10).AddMinutes(10),
                Utc(10).AddSeconds(1));

            Assert.Equal(-1.0 / 60.0, value, precision: 6);
        }

        [Fact]
        public async Task ReconcileAsync_ChangingPenaltyClosesOldIntervalAndOpensNewOne()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = CreateService(context);

            await service.ReconcileAsync(true, -0.2, hasActiveActivity: false, Utc(10));
            await service.ReconcileAsync(true, -0.5, hasActiveActivity: false, Utc(10).AddMinutes(5));

            var rows = await context.GetRowsAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal(Utc(10).AddMinutes(5), StrictTimeSerializer.ParseUtcInstant(rows[0].End!));
            Assert.Null(rows[1].End);

            var value = await service.GetValueAsync(
                Utc(10),
                Utc(10).AddMinutes(7),
                Utc(10).AddMinutes(7));

            Assert.Equal(-2.0, value, precision: 6);
        }

        [Fact]
        public async Task ReconcileAsync_UsesCurrentSettingsAndActivityState()
        {
            await using var context = new TestSqliteConnectionContext();
            var activity = new FakeActivityService();
            var service = new SqliteHardModePenaltyService(context, activity, new TimeZoneService());
            SettingsProvider.Initialize(new List<AcquiredSetting>
            {
                BoolSetting(SettingKeys.HardModeEnabled, true),
                DoubleSetting(SettingKeys.HardModeDamagePerMinuteValue, -0.75)
            });

            await service.ReconcileAsync(Utc(9));

            var row = Assert.Single(await context.GetRowsAsync());
            Assert.Equal(-0.75, row.ValuePerMinute);
            Assert.Null(row.End);
        }

        private static SqliteHardModePenaltyService CreateService(TestSqliteConnectionContext context)
        {
            return new SqliteHardModePenaltyService(context, new FakeActivityService(), new TimeZoneService());
        }

        private static DateTime Utc(int hour)
        {
            return new DateTime(2026, 1, 1, hour, 0, 0, DateTimeKind.Utc);
        }

        private static AcquiredSetting BoolSetting(string key, bool value)
        {
            return new AcquiredSetting
            {
                SettingKey = key,
                ValueType = SettingValueTypes.Bool,
                RawValue = value ? "true" : "false",
                BoolValue = value
            };
        }

        private static AcquiredSetting DoubleSetting(string key, double value)
        {
            return new AcquiredSetting
            {
                SettingKey = key,
                ValueType = SettingValueTypes.Double,
                RawValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DoubleValue = value
            };
        }

        private sealed class FakeActivityService : IActivityService
        {
            public ActivityModel? CurrentActivity { get; set; }

            public Task<ActivityModel?> GetCurrentActiveActivityAsync()
            {
                return Task.FromResult(CurrentActivity);
            }

            public Task<ToggleActivityModelResult> ToggleActivityAsync(
                long cardId,
                DateTime utcNow,
                string valueRateName,
                double valuePerMinute)
            {
                throw new NotImplementedException();
            }

            public Task<bool> HasActivityOverlapAsync(int excludeActivityId, DateTime candidateStart, DateTime? candidateEnd)
            {
                throw new NotImplementedException();
            }

            public Task<ActivityUpdateResult> UpsertActivitiesAsync(List<ActivityModel> activities, long? replaceCardId = null)
            {
                throw new NotImplementedException();
            }

            public Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId)
            {
                throw new NotImplementedException();
            }

            public Task<DateTime?> GetLastClosedActivityEndUtcAsync()
            {
                throw new NotImplementedException();
            }

            public Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsHardModePenaltyServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS HardModePenaltyInterval (
                        HardModePenaltyIntervalID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Start TEXT NOT NULL,
                        "End" TEXT NULL,
                        ValuePerMinute REAL NOT NULL,
                        CHECK ("End" IS NULL OR Start <= "End")
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS UX_HardModePenalty_OneOpen
                    ON HardModePenaltyInterval(1) WHERE "End" IS NULL;
                    """);
            }

            public async Task<List<HardModePenaltyIntervalRow>> GetRowsAsync()
            {
                await InitializeAsync();

                return await Db.QueryAsync<HardModePenaltyIntervalRow>(
                    @"SELECT HardModePenaltyIntervalID, Start, ""End"", ValuePerMinute
                      FROM HardModePenaltyInterval
                      ORDER BY HardModePenaltyIntervalID;");
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

        public sealed class HardModePenaltyIntervalRow
        {
            public int HardModePenaltyIntervalID { get; set; }
            public string Start { get; set; } = "";
            public string? End { get; set; }
            public double ValuePerMinute { get; set; }
        }
    }
}
