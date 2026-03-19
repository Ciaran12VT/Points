using System.Globalization;
using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Repositories.Interfaces;
using SQLite;

namespace Points.Services.Sqlite
{
    public sealed partial class LockRepository : SqliteRepositoryBase, ILockRepository
    {
        public LockRepository(ISqliteConnectionManager connectionManager) : base(connectionManager)
        {
        }

        public async Task<List<LockModel>> GetLocksForCardAsync(long cardId)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var lockRows = await Db.QueryAsync<LockRow>(
                @"SELECT LockId, LockNumber, CardId, TimeWindowStart, TimeWindowEnd
                  FROM Lock
                  WHERE CardId = ?
                  ORDER BY LockNumber ASC;",
                cardId).ConfigureAwait(false);

            if (lockRows.Count == 0)
                return new List<LockModel>();

            var lockIds = lockRows.Select(x => x.LockId).ToArray();

            var scheduleRows = await QueryByIdsAsync<LockScheduleRow>(
                tableName: "LockSchedule",
                idColumn: "LockId",
                ids: lockIds,
                selectColumns: "ScheduleId, LockId, FrequencyType, FrequencyValue, FromDateTime, ToDateTime",
                orderBy: "LockId ASC, ScheduleId ASC").ConfigureAwait(false);

            var dependencyRows = await QueryByIdsAsync<LockTaskDependencyRow>(
                tableName: "LockTaskDependency",
                idColumn: "LockId",
                ids: lockIds,
                selectColumns: "LockTaskDependencyId, LockId, TaskDependencyCardId, MetricType, TimeScope, GoalValue, GoalValence",
                orderBy: "LockId ASC, LockTaskDependencyId ASC").ConfigureAwait(false);

            var schedulesByLock = scheduleRows
                .GroupBy(x => x.LockId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var dependenciesByLock = dependencyRows
                .GroupBy(x => x.LockId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<LockModel>(lockRows.Count);

            foreach (var lockRow in lockRows)
            {
                schedulesByLock.TryGetValue(lockRow.LockId, out var schedules);
                dependenciesByLock.TryGetValue(lockRow.LockId, out var dependencies);

                result.Add(LockMapper.ToDomain(
                    lockRow,
                    schedules ?? Enumerable.Empty<LockScheduleRow>(),
                    dependencies ?? Enumerable.Empty<LockTaskDependencyRow>()));
            }

            return result;
        }

        public async Task SaveLocksForCardAsync(long cardId, List<LockModel> locksToSave)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            locksToSave ??= new List<LockModel>();

            foreach (var l in locksToSave)
                l.CardId = cardId;

            await Db.RunInTransactionAsync(conn =>
            {
                var existingLockIds = conn.Query<LockRow>(
                    @"SELECT LockId, LockNumber, CardId, TimeWindowStart, TimeWindowEnd
                      FROM Lock
                      WHERE CardId = ?;",
                    cardId)
                    .Select(x => x.LockId)
                    .ToList();

                if (existingLockIds.Count > 0)
                {
                    DeleteByIds(conn, "LockSchedule", "LockId", existingLockIds);
                    DeleteByIds(conn, "LockTaskDependency", "LockId", existingLockIds);

                    conn.Execute(@"DELETE FROM Lock WHERE CardId = ?;", cardId);
                }

                foreach (var model in locksToSave.OrderBy(x => x.LockNumber))
                {
                    conn.Execute(
                        @"INSERT INTO Lock (LockNumber, CardId, TimeWindowStart, TimeWindowEnd)
                          VALUES (?, ?, ?, ?);",
                        model.LockNumber,
                        cardId,
                        model.TimeWindowStart.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                        model.TimeWindowEnd.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

                    var newLockId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                    model.LockId = newLockId;

                    if (model.Schedules != null)
                    {
                        foreach (var s in model.Schedules)
                        {
                            conn.Execute(
                                @"INSERT INTO LockSchedule (LockId, FrequencyType, FrequencyValue, FromDateTime, ToDateTime)
                                  VALUES (?, ?, ?, ?, ?);",
                                newLockId,
                                s.FrequencyType.ToString(),
                                s.FrequencyValue,
                                s.FromDateTime.ToString("o", CultureInfo.InvariantCulture),
                                s.ToDateTime?.ToString("o", CultureInfo.InvariantCulture));

                            s.LockId = newLockId;
                        }
                    }

                    if (model.Dependencies != null)
                    {
                        foreach (var d in model.Dependencies)
                        {
                            conn.Execute(
                                @"INSERT INTO LockTaskDependency
                                    (LockId, TaskDependencyCardId, MetricType, TimeScope, GoalValue, GoalValence)
                                  VALUES (?, ?, ?, ?, ?, ?);",
                                newLockId,
                                d.TaskDependencyCardId,
                                (int)d.MetricType,
                                (int)d.TimeScope,
                                d.GoalValue,
                                (int)d.GoalValence);

                            d.LockId = newLockId;
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        public async Task DeleteLockModelAsync(LockModel model)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(model);

            var lockId = model.LockId;
            var hasPk = lockId > 0;

            if (!hasPk)
            {
                if (model.CardId <= 0)
                    throw new InvalidOperationException("Cannot delete Lock without LockId or a valid CardId.");

                if (model.LockNumber <= 0)
                    throw new InvalidOperationException("Cannot delete Lock without LockId or a valid LockNumber.");

                lockId = await Db.ExecuteScalarAsync<long>(
                    @"SELECT LockId
                      FROM Lock
                      WHERE CardId = ? AND LockNumber = ?
                      LIMIT 1;",
                    model.CardId,
                    model.LockNumber).ConfigureAwait(false);

                if (lockId <= 0)
                    return;
            }

            await Db.RunInTransactionAsync(conn =>
            {
                conn.Execute(@"DELETE FROM LockSchedule WHERE LockId = ?;", lockId);
                conn.Execute(@"DELETE FROM LockTaskDependency WHERE LockId = ?;", lockId);
                conn.Execute(@"DELETE FROM Lock WHERE LockId = ?;", lockId);
            }).ConfigureAwait(false);
        }

        private async Task<List<T>> QueryByIdsAsync<T>(
            string tableName,
            string idColumn,
            IEnumerable<long> ids,
            string selectColumns,
            string? orderBy = null)
            where T : new()
        {
            var idList = ids?.Distinct().ToList() ?? new List<long>();
            if (idList.Count == 0)
                return new List<T>();

            var placeholders = string.Join(", ", idList.Select(_ => "?"));

            var sql = $@"
                SELECT {selectColumns}
                FROM {tableName}
                WHERE {idColumn} IN ({placeholders})";

            if (!string.IsNullOrWhiteSpace(orderBy))
                sql += $" ORDER BY {orderBy}";

            sql += ";";

            return await Db.QueryAsync<T>(sql, idList.Cast<object>().ToArray()).ConfigureAwait(false);
        }

        private static void DeleteByIds(
            SQLiteConnection conn,
            string tableName,
            string idColumn,
            IReadOnlyCollection<long> ids)
        {
            if (ids == null || ids.Count == 0)
                return;

            var placeholders = string.Join(", ", ids.Select(_ => "?"));
            var sql = $"DELETE FROM {tableName} WHERE {idColumn} IN ({placeholders});";

            conn.Execute(sql, ids.Cast<object>().ToArray());
        }
    }
}