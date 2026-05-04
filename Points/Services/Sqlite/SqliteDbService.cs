using Points.Global;
using Points.Services.Settings;
using Points.Services.Persistence;
using Points.Services.Time;
using SQLite;
using SQLitePCL;

namespace Points.Services.Sqlite
{
    public class SqliteDbService : ISqliteConnectionContext, IDatabaseMaintenanceService
    {
        private readonly string _dbPath;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IClock _clock;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);

        private SQLiteAsyncConnection? _db;

        public string DatabasePath => _dbPath;

        public SQLiteAsyncConnection Db => _db ?? throw new InvalidOperationException("DB not initialized.");

        public SqliteDbService()
            : this(new TimeZoneService(), new SystemClock())
        {
        }

        public SqliteDbService(ITimeZoneService timeZoneService)
            : this(timeZoneService, new SystemClock())
        {
        }

        public SqliteDbService(ITimeZoneService timeZoneService, IClock clock)
        {
            _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _dbPath = AppPaths.DatabasePath;
        }

        public async Task InitializeAsync()
        {
            if (_db != null)
                return;

            await _initSemaphore.WaitAsync();
            try
            {
                if (_db != null)
                    return;

                Batteries_V2.Init();

                _db = new SQLiteAsyncConnection(
                    _dbPath,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

                await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");

                var script = SqlQueryService.GenerateDbCreationScript();
                var statements = script
                    .Split(';')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                await _db.RunInTransactionAsync(conn =>
                {
                    conn.Execute("PRAGMA foreign_keys = ON;");
                    foreach (var stmt in statements)
                        conn.Execute(stmt);
                });

                await EnsureCardSchemaAsync();
                await EnsureMissionCardSchemaAsync();
                await EnsureGoalSchemaAsync();
                await EnsureAchievementCardSchemaAsync();
                await EnsureTrackerCardSchemaAsync();
                await EnsureTimeHandlingMigrationAsync();
                await SaveBuiltInSettingDefinitionsAsync();
            }
            finally
            {
                _initSemaphore.Release();
            }
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

        public async Task WipeAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("DB not initialized.");

            var script = SqlQueryService.GenerateDbWipeDataScript();
            var statements = script
                .Split(';')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Where(s => !s.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase))
                .ToList();

            await _db.ExecuteAsync("PRAGMA foreign_keys = OFF;");

            try
            {
                await _db.RunInTransactionAsync(conn =>
                {
                    foreach (var stmt in statements)
                        conn.Execute(stmt);
                });
            }
            finally
            {
                await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
            }
        }

        public async Task CloseDatabaseAsync()
        {
            if (_db == null)
                return;

            try
            {
                await _db.CloseAsync();
            }
            catch
            {
                // Ignore: connection may already be closed.
            }

            _db = null;
        }

        public async Task ReinitializeDatabaseAsync()
        {
            await CloseDatabaseAsync();

            _db = new SQLiteAsyncConnection(
                _dbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
        }

        private async Task EnsureGoalSchemaAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            await MigrateGoalTableAsync();
            await RenameColumnIfNeededAsync("AchievementCard", "GoalType", "TargetType");
            await RenameColumnIfNeededAsync("LockTaskDependency", "GoalValue", "TargetValue");
            await RenameColumnIfNeededAsync("LockTaskDependency", "GoalValence", "TargetValence");
            await MigrateSettingKeyAsync("PlannersActive", "GoalsActive");
            await MigrateSettingKeyAsync("PlannersScreenOrder", "GoalsScreenOrder");
        }

        private async Task EnsureCardSchemaAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            var cols = await Db.QueryAsync<PragmaTableInfo>("PRAGMA table_info(Card);");
            var existing = cols
                .Select(c => c.name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (existing.Contains("DisplayOrder"))
                return;

            await Db.ExecuteAsync("ALTER TABLE Card ADD COLUMN DisplayOrder INTEGER NOT NULL DEFAULT 0;");
            await Db.ExecuteAsync("UPDATE Card SET DisplayOrder = CardID WHERE DisplayOrder = 0;");
        }

        private async Task EnsureMissionCardSchemaAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            await MissionCardSchemaMigration.EnsureAsync(Db);
        }

        private async Task MigrateGoalTableAsync()
        {
            if (!await TableExistsAsync("PlannerGoal"))
                return;

            await Db.ExecuteAsync(@"
                INSERT OR IGNORE INTO Goal
                    (GoalID, CardID, TimeScope, GoalHrs, Enabled, DeFactoStart, DeFactoEnd)
                SELECT
                    PlannerGoalID, CardID, TimeScope, GoalHrs, Enabled, DeFactoStart, DeFactoEnd
                FROM PlannerGoal;
            ");

            await Db.ExecuteAsync("DROP TABLE PlannerGoal;");
            await Db.ExecuteAsync("DROP INDEX IF EXISTS IX_PlannerGoal_CardID;");
            await Db.ExecuteAsync("DROP INDEX IF EXISTS IX_PlannerGoal_Enabled;");
            await Db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Goal_CardID ON Goal(CardID);");
            await Db.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Goal_Enabled ON Goal(Enabled);");
        }

        private async Task RenameColumnIfNeededAsync(string tableName, string oldColumnName, string newColumnName)
        {
            var cols = await Db.QueryAsync<PragmaTableInfo>($"PRAGMA table_info({tableName});");
            var existing = cols
                .Select(c => c.name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existing.Contains(oldColumnName) || existing.Contains(newColumnName))
                return;

            await Db.ExecuteAsync($"ALTER TABLE {tableName} RENAME COLUMN {oldColumnName} TO {newColumnName};");
        }

        private async Task<bool> TableExistsAsync(string tableName)
        {
            var count = await Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?;",
                tableName);

            return count > 0;
        }

        private async Task MigrateSettingKeyAsync(string oldKey, string newKey)
        {
            var oldExists = await Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Setting WHERE SettingKey = ?;",
                oldKey);

            if (oldExists == 0)
                return;

            var newExists = await Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Setting WHERE SettingKey = ?;",
                newKey);

            if (newExists == 0)
            {
                await Db.ExecuteAsync(
                    "UPDATE Setting SET SettingKey = ? WHERE SettingKey = ?;",
                    newKey,
                    oldKey);
                return;
            }

            var oldValue = await Db.ExecuteScalarAsync<string?>(
                "SELECT SettingValue FROM Setting WHERE SettingKey = ?;",
                oldKey);

            await Db.ExecuteAsync(
                "UPDATE Setting SET SettingValue = ? WHERE SettingKey = ?;",
                oldValue ?? "",
                newKey);

            await Db.ExecuteAsync("DELETE FROM Setting WHERE SettingKey = ?;", oldKey);
        }

        private async Task EnsureAchievementCardSchemaAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            var cols = await Db.QueryAsync<PragmaTableInfo>("PRAGMA table_info(AchievementCard);");
            var existing = cols
                .Select(c => c.name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var alterStatements = new List<string>();

            if (!existing.Contains("DeadlineStart"))
                alterStatements.Add("ALTER TABLE AchievementCard ADD COLUMN DeadlineStart TEXT NULL;");

            if (!existing.Contains("FinalizedAt"))
                alterStatements.Add("ALTER TABLE AchievementCard ADD COLUMN FinalizedAt TEXT NULL;");

            if (!existing.Contains("FrozenCurrentValue"))
                alterStatements.Add("ALTER TABLE AchievementCard ADD COLUMN FrozenCurrentValue REAL NULL;");

            foreach (var sql in alterStatements)
                await Db.ExecuteAsync(sql);
        }

        private async Task EnsureTrackerCardSchemaAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            await AddColumnIfMissingAsync("ValueTrackerCard", "Status", "TEXT NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync("EventTrackerCard", "Status", "TEXT NOT NULL DEFAULT ''");
        }

        private async Task AddColumnIfMissingAsync(string tableName, string columnName, string definition)
        {
            var cols = await Db.QueryAsync<PragmaTableInfo>($"PRAGMA table_info({tableName});");
            var existing = cols
                .Select(c => c.name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (existing.Contains(columnName))
                return;

            await Db.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
        }

        private async Task EnsureTimeHandlingMigrationAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database must be initialized before schema migration.");

            var migration = new TimeHandlingDatabaseMigration(Db, _timeZoneService, _clock);
            await migration.RunAsync();
        }

        private Task SaveBuiltInSettingDefinitionsAsync()
        {
            return new SqliteSettingsService(this).SaveBuiltInSettingDefinitionsAsync(initializeContext: false);
        }

        private sealed class PragmaTableInfo
        {
            public int cid { get; set; }
            public string name { get; set; } = "";
            public string type { get; set; } = "";
            public int notnull { get; set; }
            public string? dflt_value { get; set; }
            public int pk { get; set; }
        }
    }
}
