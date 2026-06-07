using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Goals
{
    public sealed class SqliteGoalService : IGoalService
    {
        private readonly ISqliteConnectionContext _context;

        public SqliteGoalService(ISqliteConnectionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<GoalDetailsModel>> GetGoalModelsDataAsync()
        {
            await _context.InitializeAsync();

            const string sql = @"
                SELECT
                    CardID       AS CardID,
                    TimeScope    AS TimeScope,
                    GoalHrs      AS GoalHrs,
                    Enabled      AS Enabled,
                    DeFactoStart AS DeFactoStart,
                    DeFactoEnd   AS DeFactoEnd
                FROM Goal
                ORDER BY CardID, TimeScope;
            ";

            var rows = await _context.Db.QueryAsync<GoalRow>(sql);
            if (rows.Count == 0)
                return new List<GoalDetailsModel>();

            return rows.Select(ToDomain).ToList();
        }

        public async Task SaveGoalModelsDataAsync(List<GoalDetailsModel> goalModelsToSave)
        {
            if (goalModelsToSave == null)
                throw new ArgumentNullException(nameof(goalModelsToSave));

            var normalized = goalModelsToSave
                .Where(x => x != null)
                .GroupBy(x => new { x.CardId, x.TimeScope })
                .Select(g => g.First())
                .ToList();

            if (normalized.Count == 0)
                return;

            var scope = normalized[0].TimeScope;
            if (normalized.Any(x => x.TimeScope != scope))
                throw new InvalidOperationException("SaveGoalModelsDataAsync expects a single TimeScope per call.");

            await _context.RunInTransactionAsync(conn =>
            {
                conn.Execute("DROP TABLE IF EXISTS _GoalCardKeys;");
                conn.Execute(@"
                    CREATE TEMP TABLE _GoalCardKeys
                    (
                        CardID INTEGER NOT NULL PRIMARY KEY
                    );
                ");

                const string insertKeySql = "INSERT OR IGNORE INTO _GoalCardKeys (CardID) VALUES (?);";
                foreach (var model in normalized)
                    conn.Execute(insertKeySql, model.CardId);

                conn.Execute(@"
                    DELETE FROM Goal
                    WHERE TimeScope = ?
                      AND NOT EXISTS (
                          SELECT 1
                          FROM _GoalCardKeys k
                          WHERE k.CardID = Goal.CardID
                      );
                ", scope.ToString());

                conn.Execute("DROP TABLE IF EXISTS _GoalCardKeys;");

                const string upsertSql = @"
                    INSERT INTO Goal (CardID, TimeScope, GoalHrs, Enabled, DeFactoStart, DeFactoEnd)
                    VALUES (?, ?, ?, ?, ?, ?)
                    ON CONFLICT(CardID, TimeScope) DO UPDATE SET
                        GoalHrs = excluded.GoalHrs,
                        Enabled = excluded.Enabled,
                        DeFactoStart = excluded.DeFactoStart,
                        DeFactoEnd = excluded.DeFactoEnd;
                ";

                foreach (var model in normalized)
                {
                    conn.Execute(
                        upsertSql,
                        model.CardId,
                        model.TimeScope.ToString(),
                        model.GoalHrs,
                        model.Enabled ? 1 : 0,
                        ToDbTimeOnly(model.DeFactoStart),
                        ToDbTimeOnly(model.DeFactoEnd));
                }
            });
        }

        private static GoalDetailsModel ToDomain(GoalRow row)
        {
            return new GoalDetailsModel
            {
                CardId = row.CardID,
                TimeScope = Enum.TryParse<TimeScope>(row.TimeScope, out var timeScope) ? timeScope : TimeScope.Daily,
                GoalHrs = row.GoalHrs,
                Enabled = row.Enabled != 0,
                DeFactoStart = ParseNullableTimeOnly(row.DeFactoStart),
                DeFactoEnd = ParseNullableTimeOnly(row.DeFactoEnd)
            };
        }

        private static string? ToDbTimeOnly(TimeOnly? value)
        {
            return value.HasValue ? StrictTimeSerializer.SerializeLocalTime(value.Value) : null;
        }

        private static TimeOnly? ParseNullableTimeOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return LegacyTimeReader.ReadLocalTime(value).LocalTime;
        }

        private sealed class GoalRow
        {
            public long CardID { get; set; }
            public string TimeScope { get; set; } = "";
            public double GoalHrs { get; set; }
            public int Enabled { get; set; }
            public string? DeFactoStart { get; set; }
            public string? DeFactoEnd { get; set; }
        }
    }
}
