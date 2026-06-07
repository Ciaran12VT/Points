using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Udmd;
using SQLite;
using SQLitePCL;
using Xunit;

namespace Points.Tests.Udmd
{
    public sealed class SqliteUdmdServiceTests
    {
        [Fact]
        public async Task SaveUdmdConfigAsync_InsertsTrimsNormalizesAndUpdatesConfig()
        {
            await using var context = new TestSqliteConnectionContext();
            await context.InsertCardAsync(101);
            var service = new SqliteUdmdService(context);

            var config = await service.SaveUdmdConfigAsync(new UdmdConfigModel
            {
                CardID = 101,
                FieldName = " Status ",
                FieldType = "dropdown",
                IsRequired = true,
                DisplayOrder = 2
            });

            Assert.True(config.UdmdConfigID > 0);
            Assert.Equal("Status", config.FieldName);
            Assert.Equal(UdmdFieldType.Dropdown.ToString(), config.FieldType);

            config.FieldName = " Effort ";
            config.FieldType = "number";
            config.IsRequired = false;
            config.DisplayOrder = 5;
            config.IsActive = false;

            await service.SaveUdmdConfigAsync(config);

            var saved = Assert.Single(await service.GetUdmdConfigsForCardAsync(101));
            Assert.Equal(config.UdmdConfigID, saved.UdmdConfigID);
            Assert.Equal("Effort", saved.FieldName);
            Assert.Equal(UdmdFieldType.Number.ToString(), saved.FieldType);
            Assert.False(saved.IsRequired);
            Assert.Equal(5, saved.DisplayOrder);
            Assert.False(saved.IsActive);
        }

        [Fact]
        public async Task GetActiveUdmdConfigsForCardAsync_ReturnsOnlyActiveConfigsOrdered()
        {
            await using var context = new TestSqliteConnectionContext();
            await context.InsertCardAsync(101);
            var service = new SqliteUdmdService(context);

            await service.SaveUdmdConfigAsync(new UdmdConfigModel { CardID = 101, FieldName = "Later", DisplayOrder = 2, IsActive = true });
            await service.SaveUdmdConfigAsync(new UdmdConfigModel { CardID = 101, FieldName = "Inactive", DisplayOrder = 1, IsActive = false });
            await service.SaveUdmdConfigAsync(new UdmdConfigModel { CardID = 101, FieldName = "Earlier", DisplayOrder = 1, IsActive = true });

            var configs = await service.GetActiveUdmdConfigsForCardAsync(101);

            Assert.Equal(new[] { "Earlier", "Later" }, configs.Select(x => x.FieldName));
        }

        [Fact]
        public async Task SaveDropdownValuesAsync_NormalizesDistinctValuesAndDeactivatesStaleRows()
        {
            await using var context = new TestSqliteConnectionContext();
            await context.InsertCardAsync(101);
            var service = new SqliteUdmdService(context);
            var config = await service.SaveUdmdConfigAsync(new UdmdConfigModel
            {
                CardID = 101,
                FieldName = "Priority",
                FieldType = UdmdFieldType.Dropdown.ToString()
            });

            await service.SaveDropdownValuesAsync(config.UdmdConfigID, new[] { " Low ", "low", "High", "" });

            var active = await service.GetDropdownValuesAsync(config.UdmdConfigID);
            Assert.Equal(new[] { "Low", "High" }, active.Select(x => x.DropdownValue));
            Assert.Equal(new[] { 0, 1 }, active.Select(x => x.DisplayOrder));

            await service.SaveDropdownValuesAsync(config.UdmdConfigID, new[] { "High" });

            active = await service.GetDropdownValuesAsync(config.UdmdConfigID);
            var savedRows = await context.GetDropdownRowsAsync(config.UdmdConfigID);
            Assert.Equal("High", Assert.Single(active).DropdownValue);
            Assert.Contains(savedRows, row => row.DropdownValue == "Low" && row.IsActive == 0);
            Assert.Contains(savedRows, row => row.DropdownValue == "High" && row.IsActive == 1);
        }

        [Fact]
        public async Task SaveMetadataForEntityAsync_ValidatesRequiredAndUpsertsNormalizedValues()
        {
            await using var context = new TestSqliteConnectionContext();
            await context.InsertCardAsync(101);
            var activityId = await context.InsertActivityAsync(101);
            var service = new SqliteUdmdService(context);
            var mood = await service.SaveUdmdConfigAsync(new UdmdConfigModel
            {
                CardID = 101,
                FieldName = "Mood",
                FieldType = UdmdFieldType.Text.ToString(),
                IsRequired = true
            });
            var effort = await service.SaveUdmdConfigAsync(new UdmdConfigModel
            {
                CardID = 101,
                FieldName = "Effort",
                FieldType = UdmdFieldType.Number.ToString()
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveActivityMetadataAsync(
                    101,
                    activityId,
                    new[] { new UdmdValueInput { UdmdConfigID = effort.UdmdConfigID, FieldValue = "1" } }));

            await service.SaveActivityMetadataAsync(
                101,
                activityId,
                new[]
                {
                    new UdmdValueInput { UdmdConfigID = mood.UdmdConfigID, FieldValue = " good " },
                    new UdmdValueInput { UdmdConfigID = effort.UdmdConfigID, FieldValue = "1.5" }
                });

            var metadata = (await service.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.Activity, activityId))
                .ToDictionary(x => x.FieldName, x => x.FieldValue);
            Assert.Equal("good", metadata["Mood"]);
            Assert.Equal("1.5", metadata["Effort"]);

            await service.SaveActivityMetadataAsync(
                101,
                activityId,
                new[]
                {
                    new UdmdValueInput { UdmdConfigID = mood.UdmdConfigID, FieldValue = "great" },
                    new UdmdValueInput { UdmdConfigID = effort.UdmdConfigID, FieldValue = "2" }
                });

            metadata = (await service.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.Activity, activityId))
                .ToDictionary(x => x.FieldName, x => x.FieldValue);
            Assert.Equal(2, await context.CountMetadataRowsAsync());
            Assert.Equal("great", metadata["Mood"]);
            Assert.Equal("2", metadata["Effort"]);
        }

        [Fact]
        public async Task SaveMetadataForEntityAsync_RejectsParentRowsForDifferentCards()
        {
            await using var context = new TestSqliteConnectionContext();
            await context.InsertCardAsync(101);
            await context.InsertCardAsync(202);
            var otherActivityId = await context.InsertActivityAsync(202);
            var service = new SqliteUdmdService(context);
            var mood = await service.SaveUdmdConfigAsync(new UdmdConfigModel
            {
                CardID = 101,
                FieldName = "Mood",
                FieldType = UdmdFieldType.Text.ToString()
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveActivityMetadataAsync(
                    101,
                    otherActivityId,
                    new[] { new UdmdValueInput { UdmdConfigID = mood.UdmdConfigID, FieldValue = "good" } }));
        }

        [Fact]
        public async Task SaveMetadataForEntityAsync_NormalizesDateValuesWithStrictLocalDateTimeFormat()
        {
            await using var context = new TestSqliteConnectionContext();
            await context.InsertCardAsync(101);
            var activityId = await context.InsertActivityAsync(101);
            var service = new SqliteUdmdService(context);
            var dateField = await service.SaveUdmdConfigAsync(new UdmdConfigModel
            {
                CardID = 101,
                FieldName = "Observed",
                FieldType = UdmdFieldType.Date.ToString()
            });

            await service.SaveActivityMetadataAsync(
                101,
                activityId,
                new[] { new UdmdValueInput { UdmdConfigID = dateField.UdmdConfigID, FieldValue = "2026-04-29" } });

            var metadata = Assert.Single(await service.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.Activity, activityId));
            Assert.Equal("2026-04-29T00:00:00.0000000", metadata.FieldValue);
        }

        private sealed class TestSqliteConnectionContext : ISqliteConnectionContext, IAsyncDisposable
        {
            private SQLiteAsyncConnection? _db;

            public TestSqliteConnectionContext()
            {
                DatabasePath = Path.Combine(
                    Path.GetTempPath(),
                    $"PointsUdmdServiceTests-{Guid.NewGuid():N}.db");
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
                    CREATE TABLE IF NOT EXISTS Activity (
                        ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardID INTEGER NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS BudgetCard (
                        BudgetCardID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardID INTEGER NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS BudgetCardTransaction (
                        BudgetCardTransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                        BudgetCardID INTEGER NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS TrackerValue (
                        TrackerValueID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardID INTEGER NOT NULL
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS UdmdConfig (
                        UdmdConfigID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardID INTEGER NOT NULL,
                        FieldName TEXT NOT NULL,
                        FieldType TEXT NOT NULL,
                        IsRequired INTEGER NOT NULL DEFAULT 0,
                        DisplayOrder INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1
                    );
                    """);
                await _db.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS UdmdDropdown (
                        UdmdDropdownID INTEGER PRIMARY KEY AUTOINCREMENT,
                        UdmdConfigID INTEGER NOT NULL,
                        DropdownValue TEXT NOT NULL,
                        DisplayOrder INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1
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

            public async Task InsertCardAsync(long cardId)
            {
                await InitializeAsync();
                await Db.ExecuteAsync("INSERT INTO Card (CardID) VALUES (?);", cardId);
            }

            public async Task<long> InsertActivityAsync(long cardId)
            {
                await InitializeAsync();
                await Db.ExecuteAsync("INSERT INTO Activity (CardID) VALUES (?);", cardId);
                return await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }

            public async Task<List<DropdownRow>> GetDropdownRowsAsync(long udmdConfigId)
            {
                await InitializeAsync();
                return await Db.QueryAsync<DropdownRow>(
                    @"SELECT DropdownValue, IsActive
                      FROM UdmdDropdown
                      WHERE UdmdConfigID = ?
                      ORDER BY DisplayOrder, DropdownValue;",
                    udmdConfigId);
            }

            public async Task<int> CountMetadataRowsAsync()
            {
                await InitializeAsync();
                return await Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM UdmdTrans;");
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

                try
                {
                    if (File.Exists(DatabasePath))
                        File.Delete(DatabasePath);
                }
                catch
                {
                }
            }

            public sealed class DropdownRow
            {
                public string DropdownValue { get; set; } = "";
                public int IsActive { get; set; }
            }
        }
    }
}
