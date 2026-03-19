using System.Globalization;
using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Repositories.Interfaces;

namespace Points.Services.Sqlite
{
    public sealed partial class ActivityRepository : SqliteRepositoryBase, IActivityRepository
    {
        public ActivityRepository(ISqliteConnectionManager connectionManager)
            : base(connectionManager)
        {
        }

        public async Task<ActivityModel?> GetCurrentActiveActivityAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var row = await GetCurrentActiveRowAsync().ConfigureAwait(false);
            if (row == null)
                return null;

            return ActivityMapper.ToModel(row, ParseIsoDateTime);
        }

        public async Task<ToggleActivityModelResult> ToggleActivityAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var rowResult = await ToggleActivityInternalAsync(
                cardId,
                utcNow,
                valueRateName,
                valuePerMinute).ConfigureAwait(false);

            return new ToggleActivityModelResult
            {
                Closed = rowResult.Closed != null
                    ? ActivityMapper.ToModel(rowResult.Closed, ParseIsoDateTime)
                    : null,

                Opened = rowResult.Opened != null
                    ? ActivityMapper.ToModel(rowResult.Opened, ParseIsoDateTime)
                    : null
            };
        }

        public async Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var repIso = DateTime.SpecifyKind(repTime, DateTimeKind.Utc).ToString("o");

            const string sql = @"
                INSERT OR REPLACE INTO ScCardStepRep (
                    ScCardStepID,
                    TimeStamp,
                    StepValue
                )
                VALUES (?, ?, ?);";

            await Db.ExecuteAsync(sql, scCardStepID, repIso, stepValue).ConfigureAwait(false);
        }

        public async Task<bool> HasActivityOverlapAsync(
            int excludeActivityId,
            DateTime candidateStart,
            DateTime? candidateEnd)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var startIso = DateTime.SpecifyKind(candidateStart, DateTimeKind.Utc).ToString("o");
            var endIso = candidateEnd.HasValue
                ? DateTime.SpecifyKind(candidateEnd.Value, DateTimeKind.Utc).ToString("o")
                : null;

            const string sql = @"
                SELECT 1
                FROM Activity db
                WHERE (? <= 0 OR db.ActivityID <> ?)
                  AND (
                        (? IS NULL OR db.Start < ?)
                        AND
                        (db.""End"" IS NULL OR ? < db.""End"")
                      )
                LIMIT 1;";

            var hit = await Db.ExecuteScalarAsync<int?>(
                sql,
                excludeActivityId, excludeActivityId,
                endIso, endIso,
                startIso).ConfigureAwait(false);

            return hit.HasValue;
        }

        public async Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT Start
                FROM Activity
                WHERE CardID = ?
                  AND ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;";

            var startIso = await Db.ExecuteScalarAsync<string?>(sql, cardId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(startIso))
                return null;

            return DateTime.Parse(
                startIso,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        public async Task<DateTime?> GetLastClosedActivityEndUtcAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT ""End""
                FROM Activity
                WHERE ""End"" IS NOT NULL
                ORDER BY ""End"" DESC
                LIMIT 1;";

            var endIso = await Db.ExecuteScalarAsync<string?>(sql).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(endIso))
                return null;

            return DateTime.Parse(
                endIso,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        private async Task<ActivityRow?> GetCurrentActiveRowAsync()
        {
            const string sql = @"
                SELECT
                    ActivityID,
                    CardID,
                    Start,
                    ""End"",
                    ValueRateName,
                    ValuePerMinute
                FROM Activity
                WHERE ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;";

            var rows = await Db.QueryAsync<ActivityRow>(sql).ConfigureAwait(false);
            var row = rows.FirstOrDefault();

            if (row == null)
                return null;

            row.End ??= string.Empty;
            return row;
        }

        private async Task<ToggleActivityRowResult> ToggleActivityInternalAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute)
        {
            var nowIso = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).ToString("o");

            ActivityRow? closed = null;
            ActivityRow? opened = null;

            await Db.RunInTransactionAsync(tran =>
            {
                closed = tran.Query<ActivityRow>(@"
                    SELECT
                        ActivityID       AS ActivityID,
                        CardID           AS CardID,
                        Start            AS Start,
                        ""End""          AS End,
                        ValueRateName    AS ValueRateName,
                        ValuePerMinute   AS ValuePerMinute
                    FROM Activity
                    WHERE ""End"" IS NULL
                    ORDER BY Start DESC
                    LIMIT 1;
                ").FirstOrDefault();

                if (closed != null)
                {
                    tran.Execute(@"
                        UPDATE Activity
                        SET ""End"" = ?
                        WHERE ActivityID = ?;",
                        nowIso,
                        closed.ActivityID);

                    closed.End = nowIso;

                    if (closed.CardID == cardId)
                        return;
                }

                tran.Execute(@"
                    INSERT INTO Activity (
                        CardID,
                        Start,
                        ""End"",
                        ValueRateName,
                        ValuePerMinute
                    )
                    VALUES (?, ?, NULL, ?, ?);",
                    cardId,
                    nowIso,
                    valueRateName ?? string.Empty,
                    valuePerMinute);

                var newId = tran.ExecuteScalar<long>("SELECT last_insert_rowid();");

                opened = new ActivityRow
                {
                    ActivityID = (int)newId,
                    CardID = cardId,
                    Start = nowIso,
                    End = null,
                    ValueRateName = valueRateName ?? string.Empty,
                    ValuePerMinute = valuePerMinute
                };
            }).ConfigureAwait(false);

            return new ToggleActivityRowResult
            {
                Closed = closed,
                Opened = opened
            };
        }

        private static DateTime ParseIsoDateTime(string isoValue)
        {
            return DateTime.Parse(
                isoValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }
    }
}