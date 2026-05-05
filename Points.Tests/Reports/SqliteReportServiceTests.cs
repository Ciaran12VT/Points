using Points.Global;
using Points.Services.Sqlite;
using Points.Models;
using Points.Models.DbModels;
using Points.Services.Reports;
using Points.Services.Persistence;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Reports
{
    public sealed class SqliteReportServiceTests
    {
        [Fact]
        public async Task UpsertReportAsync_InsertsReportAndSetsId()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteReportService(context);
            var lastRun = new DateTime(2026, 4, 29, 10, 30, 0, DateTimeKind.Utc);
            var report = new ReportModel
            {
                Title = "Daily score",
                SQLQuery = "SELECT 1",
                LastRunOn = lastRun,
                EligibleForAchievment = true
            };

            await service.UpsertReportAsync(report);

            var reports = await service.GetReportsAsync();
            Assert.True(report.Id > 0);
            var saved = Assert.Single(reports);
            Assert.Equal(report.Id, saved.Id);
            Assert.Equal("Daily score", saved.Title);
            Assert.Equal("SELECT 1", saved.SQLQuery);
            Assert.Equal(lastRun, saved.LastRunOn);
            Assert.True(saved.EligibleForAchievment);
        }

        [Fact]
        public async Task UpsertReportAsync_UpdatesExistingReportById()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteReportService(context);
            var report = new ReportModel
            {
                Title = "Original",
                SQLQuery = "SELECT 1"
            };

            await service.UpsertReportAsync(report);
            report.Title = "Updated";
            report.SQLQuery = "SELECT 2";

            await service.UpsertReportAsync(report);

            var saved = Assert.Single(await service.GetReportsAsync());
            Assert.Equal(report.Id, saved.Id);
            Assert.Equal("Updated", saved.Title);
            Assert.Equal("SELECT 2", saved.SQLQuery);
        }

        [Fact]
        public async Task DeleteReportAsync_RemovesReport()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteReportService(context);
            var report = new ReportModel
            {
                Title = "Delete me",
                SQLQuery = "SELECT 1"
            };

            await service.UpsertReportAsync(report);
            await service.DeleteReportAsync(report.Id);

            Assert.Empty(await service.GetReportsAsync());
        }

        [Fact]
        public async Task ExecuteSelectForReportAsync_ReturnsHeadersAndRowsWithParameters()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteReportService(context);
            var report = new ReportModel
            {
                Title = "Parameterized",
                SQLQuery = "SELECT 1",
                EligibleForAchievment = true
            };

            await service.UpsertReportAsync(report);

            var rows = await service.ExecuteSelectForReportAsync(
                "SELECT Title, EligibleForAchievment FROM Report WHERE Id = ?",
                true,
                report.Id);

            Assert.Equal(new[] { "Title | EligibleForAchievment", "Parameterized | 1" }, rows);
        }

        [Fact]
        public async Task ExecuteSelectForReportAsync_RejectsWrites()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteReportService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExecuteSelectForReportAsync("DELETE FROM Report"));
        }

        [Fact]
        public async Task ExecuteSelectForReportAsync_InterruptsWhenConfiguredTimeoutExpires()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteReportService(
                context,
                settings: new TestSettingsService(timeoutMilliseconds: 1));

            var ex = await Assert.ThrowsAsync<TimeoutException>(
                () => service.ExecuteSelectForReportAsync("""
                    WITH RECURSIVE numbers(value) AS (
                        SELECT 1
                        UNION ALL
                        SELECT value + 1 FROM numbers WHERE value < 100000000
                    )
                    SELECT sum(value) FROM numbers;
                    """));

            Assert.Contains("1 milliseconds", ex.Message);
        }

        private sealed class TestSettingsService : ISettingsService
        {
            private readonly int _timeoutMilliseconds;

            public TestSettingsService(int timeoutMilliseconds)
            {
                _timeoutMilliseconds = timeoutMilliseconds;
            }

            public Task<List<AcquiredSetting>> GetSettingsAsync()
            {
                return Task.FromResult(new List<AcquiredSetting>
                {
                    new()
                    {
                        SettingKey = SettingKeys.ReportQueryTimeoutMilliseconds,
                        ValueType = SettingValueTypes.Int,
                        RawValue = _timeoutMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        IntValue = _timeoutMilliseconds
                    }
                });
            }

            public Task SetStringSettingAsync(string settingKey, string value) => throw new NotSupportedException();
            public Task SetBoolSettingAsync(string settingKey, bool value) => throw new NotSupportedException();
            public Task SetIntSettingAsync(string settingKey, int value) => throw new NotSupportedException();
            public Task SetNullableIntSettingAsync(string settingKey, int? value) => throw new NotSupportedException();
            public Task SetDoubleSettingAsync(string settingKey, double value) => throw new NotSupportedException();
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsReportServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS Report (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL UNIQUE,
                        SQLQuery TEXT NOT NULL DEFAULT '',
                        LastRunOn TEXT NULL,
                        EligibleForAchievment INTEGER NOT NULL DEFAULT 0
                    );
                    """);
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
