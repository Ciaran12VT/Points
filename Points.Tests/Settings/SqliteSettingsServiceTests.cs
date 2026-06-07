using Points.Services.Sqlite;
using Points.Global;
using Points.Models.DbModels;
using Points.Services.Settings;
using Points.Services.Persistence;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Settings
{
    public sealed class SqliteSettingsServiceTests
    {
        [Fact]
        public async Task SaveBuiltInSettingDefinitionsAsync_InsertsBuiltInsPreservesValuesAndDeletesStaleRows()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteSettingsService(context);
            await context.InsertSettingAsync(SettingKeys.HardModeEnabled, "true", SettingValueTypes.Bool, category: "Old", displayName: "Old");
            await context.InsertSettingAsync("RemovedSetting", "gone", SettingValueTypes.String);

            await service.SaveBuiltInSettingDefinitionsAsync();

            var settings = await service.GetSettingsAsync();
            var hardMode = Assert.Single(settings, x => x.SettingKey == SettingKeys.HardModeEnabled);
            var username = Assert.Single(settings, x => x.SettingKey == SettingKeys.Username);
            var eventOffset = Assert.Single(settings, x => x.SettingKey == SettingKeys.MissionDefaultEventDateOffsetDays);
            var reportTimeout = Assert.Single(settings, x => x.SettingKey == SettingKeys.ReportQueryTimeoutMilliseconds);

            Assert.True(hardMode.BoolValue);
            Assert.Equal("Multipliers", hardMode.Category);
            Assert.Equal("Hard Mode", hardMode.DisplayName);
            Assert.Equal("DefaultsAndMisc", username.Category);
            Assert.Equal(SettingValueTypes.NullableInt, eventOffset.ValueType);
            Assert.Null(eventOffset.IntValue);
            Assert.Equal("Database", reportTimeout.Category);
            Assert.Equal(5000, reportTimeout.IntValue);
            Assert.DoesNotContain(settings, x => x.SettingKey == "RemovedSetting");
        }

        [Fact]
        public async Task GetSettingsAsync_ParsesTypedRows()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteSettingsService(context);
            await context.InsertSettingAsync("Text", "hello", SettingValueTypes.String);
            await context.InsertSettingAsync("Flag", "TRUE", SettingValueTypes.Bool);
            await context.InsertSettingAsync("Count", "-12", SettingValueTypes.Int);
            await context.InsertSettingAsync("Optional", "", SettingValueTypes.NullableInt);
            await context.InsertSettingAsync("Rate", "-0.25", SettingValueTypes.Double);

            var settings = await service.GetSettingsAsync();

            Assert.Equal("hello", Assert.Single(settings, x => x.SettingKey == "Text").StringValue);
            Assert.True(Assert.Single(settings, x => x.SettingKey == "Flag").BoolValue);
            Assert.Equal(-12, Assert.Single(settings, x => x.SettingKey == "Count").IntValue);
            Assert.Null(Assert.Single(settings, x => x.SettingKey == "Optional").IntValue);
            Assert.Equal(-0.25, Assert.Single(settings, x => x.SettingKey == "Rate").DoubleValue);
        }

        [Fact]
        public async Task SetTypedSettingAsync_StoresFormattedValues()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteSettingsService(context);
            await context.InsertSettingAsync("Text", "", SettingValueTypes.String);
            await context.InsertSettingAsync("Flag", "true", SettingValueTypes.Bool);
            await context.InsertSettingAsync("Count", "1", SettingValueTypes.Int);
            await context.InsertSettingAsync("Optional", "10", SettingValueTypes.NullableInt);
            await context.InsertSettingAsync("Rate", "0", SettingValueTypes.Double);

            await service.SetStringSettingAsync("Text", "updated");
            await service.SetBoolSettingAsync("Flag", false);
            await service.SetIntSettingAsync("Count", 42);
            await service.SetNullableIntSettingAsync("Optional", null);
            await service.SetDoubleSettingAsync("Rate", -1.25);

            Assert.Equal("updated", await context.GetRawValueAsync("Text"));
            Assert.Equal("false", await context.GetRawValueAsync("Flag"));
            Assert.Equal("42", await context.GetRawValueAsync("Count"));
            Assert.Equal("", await context.GetRawValueAsync("Optional"));
            Assert.Equal("-1.25", await context.GetRawValueAsync("Rate"));
        }

        [Fact]
        public async Task SetTypedSettingAsync_RejectsWrongValueType()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteSettingsService(context);
            await context.InsertSettingAsync("Flag", "true", SettingValueTypes.Bool);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetIntSettingAsync("Flag", 7));

            Assert.Contains("attempted to write it as", ex.Message);
        }

        [Fact]
        public async Task GetSettingsAsync_ThrowsForInvalidTypedValue()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteSettingsService(context);
            await context.InsertSettingAsync("Flag", "not-bool", SettingValueTypes.Bool);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetSettingsAsync());

            Assert.Contains("could not be parsed", ex.Message);
        }

        [Fact]
        public async Task GetSettingsAsync_OrdersByCategorySortOrderAndKey()
        {
            await using var context = new TestSqliteConnectionContext();
            var service = new SqliteSettingsService(context);
            await context.InsertSettingAsync("B1", "b", SettingValueTypes.String, category: "B", sortOrder: 1);
            await context.InsertSettingAsync("A2", "a2", SettingValueTypes.String, category: "A", sortOrder: 2);
            await context.InsertSettingAsync("A1", "a1", SettingValueTypes.String, category: "A", sortOrder: 1);

            var keys = (await service.GetSettingsAsync()).Select(x => x.SettingKey).ToList();

            Assert.Equal(new[] { "A1", "A2", "B1" }, keys);
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsSettingsServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS Setting (
                        SettingKey TEXT PRIMARY KEY,
                        SettingValue TEXT NOT NULL,
                        ValueType TEXT NOT NULL,
                        Category TEXT NOT NULL DEFAULT '',
                        DisplayName TEXT NOT NULL DEFAULT '',
                        Description TEXT NOT NULL DEFAULT '',
                        IsUserEditable INTEGER NOT NULL DEFAULT 1,
                        SortOrder INTEGER NOT NULL DEFAULT 0
                    );
                    """);
            }

            public async Task InsertSettingAsync(
                string key,
                string value,
                string valueType,
                string category = "Test",
                string displayName = "",
                string description = "",
                bool isUserEditable = true,
                int sortOrder = 0)
            {
                await InitializeAsync();

                await Db.ExecuteAsync(
                    @"INSERT OR REPLACE INTO Setting
                      (SettingKey, SettingValue, ValueType, Category, DisplayName, Description, IsUserEditable, SortOrder)
                      VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
                    key,
                    value,
                    valueType,
                    category,
                    displayName,
                    description,
                    isUserEditable ? 1 : 0,
                    sortOrder);
            }

            public async Task<string> GetRawValueAsync(string key)
            {
                await InitializeAsync();

                return await Db.ExecuteScalarAsync<string>(
                    "SELECT SettingValue FROM Setting WHERE SettingKey = ?;",
                    key);
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
