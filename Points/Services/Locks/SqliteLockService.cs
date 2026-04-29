using Points.Models;
using Points.Services.Scheduling;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using SQLite;
using System.Globalization;

namespace Points.Services.Locks
{
    public sealed class SqliteLockService : ILockService
    {
        private readonly ISqliteConnectionContext _context;

        public SqliteLockService(ISqliteConnectionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<LockModel>> GetLocksForCardAsync(long cardId)
        {
            await _context.InitializeAsync();

            var lockRows = await _context.Db.QueryAsync<LockRow>(
                @"SELECT LockId, LockNumber, CardId, TimeWindowStart, TimeWindowEnd
                  FROM Lock
                  WHERE CardId = ?
                  ORDER BY LockNumber ASC;",
                cardId);

            if (lockRows.Count == 0)
                return new List<LockModel>();

            var lockIds = lockRows.Select(x => x.LockId).ToArray();

            var scheduleRows = await QueryByIdsAsync<LockScheduleRow>(
                tableName: "LockSchedule",
                idColumn: "LockId",
                ids: lockIds,
                selectColumns: "ScheduleId, LockId, FrequencyType, FrequencyValue, FromDateTime, ToDateTime",
                orderBy: "LockId ASC, ScheduleId ASC");

            var dependencyRows = await QueryByIdsAsync<LockTaskDependencyRow>(
                tableName: "LockTaskDependency",
                idColumn: "LockId",
                ids: lockIds,
                selectColumns: "LockTaskDependencyId, LockId, TaskDependencyCardId, MetricType, TimeScope, TargetValue, TargetValence",
                orderBy: "LockId ASC, LockTaskDependencyId ASC");

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
            locksToSave ??= new List<LockModel>();

            foreach (var lockModel in locksToSave)
                lockModel.CardId = cardId;

            await _context.RunInTransactionAsync(conn =>
            {
                var existingLockIds = conn.Query<LockRow>(
                    @"SELECT LockId, LockNumber, CardId, TimeWindowStart, TimeWindowEnd
                      FROM Lock
                      WHERE CardId = ?;",
                    cardId).Select(x => x.LockId).ToList();

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
                        StrictTimeSerializer.SerializeLocalTime(model.TimeWindowStart),
                        StrictTimeSerializer.SerializeLocalTime(model.TimeWindowEnd));

                    var newLockId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                    model.LockId = newLockId;

                    if (model.Schedules != null)
                    {
                        foreach (var schedule in model.Schedules)
                        {
                            conn.Execute(
                                @"INSERT INTO LockSchedule (LockId, FrequencyType, FrequencyValue, FromDateTime, ToDateTime)
                                  VALUES (?, ?, ?, ?, ?);",
                                newLockId,
                                schedule.FrequencyType.ToString(),
                                schedule.FrequencyValue,
                                StrictTimeSerializer.SerializeLocalDateTime(WallClockScheduleTime.NormalizeLocal(schedule.FromDateTime)),
                                StrictTimeSerializer.SerializeNullableLocalDateTime(WallClockScheduleTime.NormalizeLocal(schedule.ToDateTime)));

                            schedule.LockId = newLockId;
                        }
                    }

                    if (model.Dependencies != null)
                    {
                        foreach (var dependency in model.Dependencies)
                        {
                            conn.Execute(
                                @"INSERT INTO LockTaskDependency
                                    (LockId, TaskDependencyCardId, MetricType, TimeScope, TargetValue, TargetValence)
                                  VALUES (?, ?, ?, ?, ?, ?);",
                                newLockId,
                                dependency.TaskDependencyCardId,
                                (int)dependency.MetricType,
                                (int)dependency.TimeScope,
                                dependency.TargetValue,
                                (int)dependency.TargetValence);

                            dependency.LockId = newLockId;
                        }
                    }
                }
            });
        }

        public async Task DeleteLockModelAsync(LockModel lockModel)
        {
            await _context.InitializeAsync();

            if (lockModel == null)
                throw new ArgumentNullException(nameof(lockModel));

            var lockId = lockModel.LockId;

            if (lockId <= 0)
            {
                if (lockModel.CardId <= 0)
                    throw new InvalidOperationException("Cannot delete Lock without LockId or a valid CardId.");

                if (lockModel.LockNumber <= 0)
                    throw new InvalidOperationException("Cannot delete Lock without LockId or a valid LockNumber.");

                lockId = await _context.Db.ExecuteScalarAsync<long>(
                    @"SELECT LockId
                      FROM Lock
                      WHERE CardId = ? AND LockNumber = ?
                      LIMIT 1;",
                    lockModel.CardId,
                    lockModel.LockNumber);

                if (lockId <= 0)
                    return;
            }

            await _context.RunInTransactionAsync(conn =>
            {
                conn.Execute(@"DELETE FROM LockSchedule WHERE LockId = ?;", lockId);
                conn.Execute(@"DELETE FROM LockTaskDependency WHERE LockId = ?;", lockId);
                conn.Execute(@"DELETE FROM Lock WHERE LockId = ?;", lockId);
            });
        }

        private async Task<List<T>> QueryByIdsAsync<T>(
            string tableName,
            string idColumn,
            long[] ids,
            string selectColumns,
            string? orderBy = null)
            where T : new()
        {
            if (ids.Length == 0)
                return new List<T>();

            var placeholders = string.Join(",", Enumerable.Repeat("?", ids.Length));
            var sql = $@"SELECT {selectColumns}
                 FROM {tableName}
                 WHERE {idColumn} IN ({placeholders})
                 {(string.IsNullOrWhiteSpace(orderBy) ? "" : $"ORDER BY {orderBy}")};";

            object[] args = ids.Cast<object>().ToArray();
            return await _context.Db.QueryAsync<T>(sql, args);
        }

        private static void DeleteByIds(SQLiteConnection conn, string table, string idColumn, List<long> ids)
        {
            const int chunkSize = 500;

            for (int i = 0; i < ids.Count; i += chunkSize)
            {
                var chunk = ids.Skip(i).Take(chunkSize).ToArray();
                var placeholders = string.Join(",", Enumerable.Repeat("?", chunk.Length));
                var sql = $"DELETE FROM {table} WHERE {idColumn} IN ({placeholders});";
                conn.Execute(sql, chunk.Cast<object>().ToArray());
            }
        }

        private static TimeOnly ParseLocalTime(string value)
        {
            if (StrictTimeSerializer.TryParseLocalTime(value, out var strictTime))
                return strictTime;

            return TimeOnly.Parse(value, CultureInfo.InvariantCulture);
        }

        private sealed class LockRow
        {
            public long LockId { get; set; }
            public int LockNumber { get; set; }
            public long CardId { get; set; }
            public string TimeWindowStart { get; set; } = "";
            public string TimeWindowEnd { get; set; } = "";
        }

        private sealed class LockScheduleRow
        {
            public long ScheduleId { get; set; }
            public long LockId { get; set; }
            public string FrequencyType { get; set; } = "";
            public int FrequencyValue { get; set; }
            public string FromDateTime { get; set; } = "";
            public string? ToDateTime { get; set; }
        }

        private sealed class LockTaskDependencyRow
        {
            public long LockTaskDependencyId { get; set; }
            public long LockId { get; set; }
            public long TaskDependencyCardId { get; set; }
            public int MetricType { get; set; }
            public int TimeScope { get; set; }
            public double TargetValue { get; set; }
            public int TargetValence { get; set; }
        }

        private static class LockMapper
        {
            public static LockModel ToDomain(
                LockRow row,
                IEnumerable<LockScheduleRow> scheduleRows,
                IEnumerable<LockTaskDependencyRow> dependencyRows)
            {
                return new LockModel
                {
                    LockId = row.LockId,
                    LockNumber = row.LockNumber,
                    CardId = row.CardId,
                    TimeWindowStart = ParseLocalTime(row.TimeWindowStart),
                    TimeWindowEnd = ParseLocalTime(row.TimeWindowEnd),
                    Schedules = scheduleRows.Select(LockScheduleMapper.ToDomain).ToList(),
                    Dependencies = dependencyRows.Select(LockTaskDependencyMapper.ToDomain).ToList(),
                };
            }
        }

        private static class LockScheduleMapper
        {
            public static LockScheduleModel ToDomain(LockScheduleRow row)
            {
                return new LockScheduleModel
                {
                    ScheduleId = row.ScheduleId,
                    LockId = row.LockId,
                    FrequencyType = (FrequencyType)Enum.Parse(typeof(FrequencyType), row.FrequencyType),
                    FrequencyValue = row.FrequencyValue,
                    FromDateTime = LegacyTimeReader.ReadLocalDateTime(row.FromDateTime).LocalDateTime,
                    ToDateTime = string.IsNullOrWhiteSpace(row.ToDateTime)
                        ? null
                        : LegacyTimeReader.ReadLocalDateTime(row.ToDateTime!).LocalDateTime,
                };
            }
        }

        private static class LockTaskDependencyMapper
        {
            public static LockTaskDependencyModel ToDomain(LockTaskDependencyRow row)
            {
                return new LockTaskDependencyModel
                {
                    LockTaskDependencyId = row.LockTaskDependencyId,
                    LockId = row.LockId,
                    TaskDependencyCardId = row.TaskDependencyCardId,
                    MetricType = (LockDependencyMetricType)row.MetricType,
                    TimeScope = (TimeScope)row.TimeScope,
                    TargetValue = row.TargetValue,
                    TargetValence = (TargetValence)row.TargetValence
                };
            }
        }
    }
}
