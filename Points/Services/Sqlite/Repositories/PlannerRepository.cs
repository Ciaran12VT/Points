using System.Globalization;
using Points.Models;
using Points.Services.Sqlite.Repositories.Interfaces;
using Points.ViewModels;

namespace Points.Services.Sqlite
{
    public sealed partial class PlannerRepository : SqliteRepositoryBase, IPlannerRepository
    {
        public PlannerRepository(ISqliteConnectionManager connectionManager) : base(connectionManager)
        {
        }

        public async Task<List<PlannerGoalDetailsModel>> GetPlannerModelsDataAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT
                    CardID       AS CardID,
                    TimeScope    AS TimeScope,
                    GoalHrs      AS GoalHrs,
                    Enabled      AS Enabled,
                    DeFactoStart AS DeFactoStart,
                    DeFactoEnd   AS DeFactoEnd
                FROM PlannerGoal
                ORDER BY CardID, TimeScope;";

            var rows = await Db.QueryAsync<PlannerGoalRow>(sql).ConfigureAwait(false);

            if (rows.Count == 0) return new List<PlannerGoalDetailsModel>();

            return rows.Select(PlannerGoalMapper.ToDomain).ToList();
        }

        public async Task SavePlannerModelsDataAsync(List<PlannerGoalDetailsModel> plannerModelsToSave)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(plannerModelsToSave);

            var normalized = plannerModelsToSave
                .Where(x => x != null)
                .GroupBy(x => new { x.CardId, x.TimeScope })
                .Select(g => g.First())
                .ToList();

            // Preserve current behavior:
            // if no rows are provided, do nothing because the current API shape
            // does not tell us which scope should be cleared.
            if (normalized.Count == 0)
                return;

            // Preserve current behavior:
            // one TimeScope per save call.
            var scope = normalized[0].TimeScope;
            if (normalized.Any(x => x.TimeScope != scope))
                throw new InvalidOperationException(
                    "SavePlannerModelsDataAsync expects a single TimeScope per call.");

            await Db.RunInTransactionAsync(conn =>
            {
                // Mirror semantics for THIS scope only:
                // remove DB rows for the scope whose CardID is no longer in the incoming set.
                conn.Execute("DROP TABLE IF EXISTS _PlannerGoalCardKeys;");
                conn.Execute(@"
                    CREATE TEMP TABLE _PlannerGoalCardKeys
                    (
                        CardID INTEGER NOT NULL PRIMARY KEY
                    );");

                const string insertKeySql = @"
                    INSERT OR IGNORE INTO _PlannerGoalCardKeys (CardID)
                    VALUES (?);";

                foreach (var model in normalized)
                {
                    conn.Execute(insertKeySql, model.CardId);
                }

                conn.Execute(@"
                    DELETE FROM PlannerGoal
                    WHERE TimeScope = ?
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM _PlannerGoalCardKeys k
                          WHERE k.CardID = PlannerGoal.CardID
                      );",
                    scope.ToString());

                conn.Execute("DROP TABLE IF EXISTS _PlannerGoalCardKeys;");

                const string upsertSql = @"
                    INSERT INTO PlannerGoal
                    (
                        CardID,
                        TimeScope,
                        GoalHrs,
                        Enabled,
                        DeFactoStart,
                        DeFactoEnd
                    )
                    VALUES (?, ?, ?, ?, ?, ?)
                    ON CONFLICT(CardID, TimeScope) DO UPDATE SET
                        GoalHrs = excluded.GoalHrs,
                        Enabled = excluded.Enabled,
                        DeFactoStart = excluded.DeFactoStart,
                        DeFactoEnd = excluded.DeFactoEnd;";

                foreach (var model in normalized)
                {
                    conn.Execute(
                        upsertSql,
                        model.CardId,
                        model.TimeScope.ToString(),
                        model.GoalHrs,
                        model.Enabled ? 1 : 0,
                        PlannerGoalMapper.ToDbTimeOnly(model.DeFactoStart),
                        PlannerGoalMapper.ToDbTimeOnly(model.DeFactoEnd));
                }
            }).ConfigureAwait(false);
        }
    }
}