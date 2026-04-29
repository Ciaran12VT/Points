using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;

namespace Points.Services.Activity
{
    public sealed class SqliteActivityService : IActivityService
    {
        private readonly ISqliteConnectionContext _context;
        private readonly ITimeZoneService _timeZoneService;

        public SqliteActivityService(ISqliteConnectionContext context, ITimeZoneService? timeZoneService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _timeZoneService = timeZoneService ?? new TimeZoneService();
        }

        public async Task<ActivityModel?> GetCurrentActiveActivityAsync()
        {
            await _context.InitializeAsync();

            var row = await GetCurrentActiveRowAsync();
            return row == null ? null : ToActivityModel(row);
        }

        public async Task<ToggleActivityModelResult> ToggleActivityAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute)
        {
            var rowResult = await ToggleActivityInternalAsync(cardId, utcNow, valueRateName, valuePerMinute);

            return new ToggleActivityModelResult
            {
                Closed = rowResult.Closed != null ? ToActivityModel(rowResult.Closed) : null,
                Opened = rowResult.Opened != null ? ToActivityModel(rowResult.Opened) : null
            };
        }

        public async Task<bool> HasActivityOverlapAsync(int excludeActivityId, DateTime candidateStart, DateTime? candidateEnd)
        {
            await _context.InitializeAsync();

            var candidate = new ActivityModel
            {
                StartDate = candidateStart,
                EndDate = candidateEnd
            };
            var (candidateStartUtc, candidateEndUtc) = GetActivityIntervalUtc(candidate, validateOrder: true);

            const string sql = @"
                    SELECT ActivityID, CardID, Start, ""End"", ValueRateName, ValuePerMinute
                    FROM Activity
                    WHERE (? <= 0 OR ActivityID <> ?);
                ";

            var rows = await _context.Db.QueryAsync<ActivityRow>(sql, excludeActivityId, excludeActivityId);

            foreach (var row in rows)
            {
                var existing = ToActivityModel(row);
                var (existingStartUtc, existingEndUtc) = GetActivityIntervalUtc(existing, validateOrder: false);

                if (ActivityIntervalsOverlap(candidateStartUtc, candidateEndUtc, existingStartUtc, existingEndUtc))
                    return true;
            }

            return false;
        }

        public async Task<ActivityUpdateResult> UpsertActivitiesAsync(List<ActivityModel> activities, long? replaceCardId = null)
        {
            await _context.InitializeAsync();

            if (activities == null)
                throw new ArgumentNullException(nameof(activities));

            if (replaceCardId.HasValue && replaceCardId.Value <= 0)
                replaceCardId = null;

            if (activities.Count == 0)
            {
                if (replaceCardId.HasValue)
                    await _context.Db.ExecuteAsync("DELETE FROM Activity WHERE CardID = ?;", replaceCardId.Value);

                return new ActivityUpdateResult { Success = true, Message = "Activities updated." };
            }

            try
            {
                if (replaceCardId.HasValue && activities.Any(a => a.CardID != replaceCardId.Value))
                {
                    return new ActivityUpdateResult
                    {
                        Success = false,
                        Message = "All replacement activities must belong to the replacement card."
                    };
                }

                if (HasInternalOverlap(activities))
                {
                    return new ActivityUpdateResult
                    {
                        Success = false,
                        Message = "Overlapping Activities cannot be written to the database"
                    };
                }

                var preparedActivities = activities
                    .Select(a =>
                    {
                        var (startUtc, endUtc) = GetActivityIntervalUtc(a, validateOrder: true);
                        return new
                        {
                            Activity = a,
                            StartUtc = startUtc,
                            EndUtc = endUtc,
                            StartIso = StrictTimeSerializer.SerializeUtcInstant(startUtc),
                            EndIso = StrictTimeSerializer.SerializeNullableUtcInstant(endUtc)
                        };
                    })
                    .ToList();

                var incomingIds = activities
                    .Where(a => a.Id > 0)
                    .Select(a => a.Id)
                    .ToHashSet();

                await _context.RunInTransactionAsync(tran =>
                {
                    var existingRows = tran.Query<ActivityRow>(@"
                        SELECT ActivityID, CardID, Start, ""End"", ValueRateName, ValuePerMinute
                        FROM Activity;
                    ");

                    foreach (var row in existingRows)
                    {
                        if (incomingIds.Contains(row.ActivityID))
                            continue;

                        if (replaceCardId.HasValue && row.CardID == replaceCardId.Value)
                            continue;

                        var existing = ToActivityModel(row);
                        var (existingStartUtc, existingEndUtc) = GetActivityIntervalUtc(existing, validateOrder: false);

                        foreach (var incoming in preparedActivities)
                        {
                            if (ActivityIntervalsOverlap(incoming.StartUtc, incoming.EndUtc, existingStartUtc, existingEndUtc))
                                throw new InvalidOperationException("Cannot overlap with existing Activities in the database.");
                        }
                    }

                    if (replaceCardId.HasValue)
                    {
                        if (incomingIds.Count > 0)
                        {
                            var placeholders = string.Join(", ", incomingIds.Select(_ => "?"));
                            var args = new List<object> { replaceCardId.Value };
                            args.AddRange(incomingIds.Cast<object>());

                            tran.Execute(
                                $"DELETE FROM Activity WHERE CardID = ? AND ActivityID NOT IN ({placeholders});",
                                args.ToArray());
                        }
                        else
                        {
                            tran.Execute("DELETE FROM Activity WHERE CardID = ?;", replaceCardId.Value);
                        }
                    }

                    foreach (var prepared in preparedActivities)
                    {
                        var activity = prepared.Activity;

                        if (activity.Id > 0)
                        {
                            tran.Execute(@"
                                UPDATE Activity
                                SET CardID = ?,
                                    Start = ?,
                                    ""End"" = ?,
                                    ValueRateName = ?,
                                    ValuePerMinute = ?
                                WHERE ActivityID = ?;
                            ",
                                activity.CardID,
                                prepared.StartIso,
                                prepared.EndIso,
                                activity.RateName ?? "",
                                activity.ValuePerMinute,
                                activity.Id);
                        }
                        else
                        {
                            tran.Execute(@"
                                INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                                VALUES (?, ?, ?, ?, ?);
                            ",
                                activity.CardID,
                                prepared.StartIso,
                                prepared.EndIso,
                                activity.RateName ?? "",
                                activity.ValuePerMinute);
                        }
                    }
                });

                return new ActivityUpdateResult { Success = true, Message = "Activities updated." };
            }
            catch (InvalidOperationException ex)
            {
                return new ActivityUpdateResult { Success = false, Message = ex.Message };
            }
            catch (ArgumentException ex)
            {
                return new ActivityUpdateResult { Success = false, Message = ex.Message };
            }
            catch (FormatException ex)
            {
                return new ActivityUpdateResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId)
        {
            await _context.InitializeAsync();

            const string sql = @"
                SELECT Start
                FROM Activity
                WHERE CardID = ?
                  AND ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;
            ";

            var startIso = await _context.Db.ExecuteScalarAsync<string?>(sql, cardId);
            if (string.IsNullOrWhiteSpace(startIso))
                return null;

            return ParseInstantUtc(startIso);
        }

        public async Task<DateTime?> GetLastClosedActivityEndUtcAsync()
        {
            await _context.InitializeAsync();

            const string sql = @"
                SELECT ""End""
                FROM Activity
                WHERE ""End"" IS NOT NULL
                ORDER BY ""End"" DESC
                LIMIT 1;
            ";

            var endIso = await _context.Db.ExecuteScalarAsync<string?>(sql);
            if (string.IsNullOrWhiteSpace(endIso))
                return null;

            return ParseInstantUtc(endIso);
        }

        public async Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue)
        {
            await _context.InitializeAsync();

            await _context.Db.ExecuteAsync(
                @"INSERT OR REPLACE INTO ScCardStepRep (ScCardStepID, TimeStamp, StepValue) VALUES (?, ?, ?);",
                scCardStepID,
                SerializeInstantForDb(repTime),
                stepValue);
        }

        private async Task<ActivityRow?> GetCurrentActiveRowAsync()
        {
            const string sql = @"
                SELECT ActivityID, CardID, Start, ""End"", ValueRateName, ValuePerMinute
                FROM Activity
                WHERE ""End"" IS NULL
                ORDER BY Start DESC
                LIMIT 1;
            ";

            var rows = await _context.Db.QueryAsync<ActivityRow>(sql);
            var row = rows.FirstOrDefault();

            if (row == null)
                return null;

            row.End ??= "";
            return row;
        }

        private async Task<ToggleActivityRowResult> ToggleActivityInternalAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute)
        {
            await _context.InitializeAsync();

            var nowIso = StrictTimeSerializer.SerializeUtcInstant(utcNow);

            ActivityRow? closed = null;
            ActivityRow? opened = null;

            await _context.RunInTransactionAsync(tran =>
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
                        WHERE ActivityID = ?;
                    ", nowIso, closed.ActivityID);

                    closed.End = nowIso;

                    if (closed.CardID == cardId)
                        return;
                }

                tran.Execute(@"
                    INSERT INTO Activity (CardID, Start, ""End"", ValueRateName, ValuePerMinute)
                    VALUES (?, ?, NULL, ?, ?);
                ", cardId, nowIso, valueRateName, valuePerMinute);

                var newId = tran.ExecuteScalar<long>("SELECT last_insert_rowid();");

                opened = new ActivityRow
                {
                    ActivityID = (int)newId,
                    CardID = cardId,
                    Start = nowIso,
                    End = null,
                    ValueRateName = valueRateName ?? "",
                    ValuePerMinute = valuePerMinute
                };
            });

            return new ToggleActivityRowResult { Closed = closed, Opened = opened };
        }

        private ActivityModel ToActivityModel(ActivityRow row)
        {
            return ActivityMapper.ToModel(row, ParseInstantUtc);
        }

        private DateTime ParseInstantUtc(string value)
        {
            return LegacyTimeReader.ReadInstantUtc(value, _timeZoneService).UtcInstant;
        }

        private DateTime ToUtcInstantForWrite(DateTime value)
        {
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
                return new DateTime(value.Ticks, DateTimeKind.Utc);

            return value.Kind == DateTimeKind.Utc
                ? StrictTimeSerializer.RequireUtcInstant(value, nameof(value))
                : _timeZoneService.ToUtcFromLocal(value);
        }

        private string SerializeInstantForDb(DateTime value)
        {
            return StrictTimeSerializer.SerializeUtcInstant(ToUtcInstantForWrite(value));
        }

        private static (DateTime StartUtc, DateTime? EndUtc) GetActivityIntervalUtc(ActivityModel activity, bool validateOrder)
        {
            if (activity == null)
                throw new ArgumentNullException(nameof(activity));

            var startUtc = StrictTimeSerializer.RequireUtcInstant(activity.StartDate, nameof(activity.StartDate));
            var endUtc = activity.EndDate.HasValue
                ? StrictTimeSerializer.RequireUtcInstant(activity.EndDate.Value, nameof(activity.EndDate))
                : (DateTime?)null;

            if (validateOrder && endUtc.HasValue && endUtc.Value <= startUtc)
                throw new InvalidOperationException("Activity end must be after start.");

            return (startUtc, endUtc);
        }

        private static bool ActivityIntervalsOverlap(
            DateTime leftStartUtc,
            DateTime? leftEndUtc,
            DateTime rightStartUtc,
            DateTime? rightEndUtc)
        {
            leftStartUtc = StrictTimeSerializer.RequireUtcInstant(leftStartUtc, nameof(leftStartUtc));
            rightStartUtc = StrictTimeSerializer.RequireUtcInstant(rightStartUtc, nameof(rightStartUtc));

            if (leftEndUtc.HasValue)
                leftEndUtc = StrictTimeSerializer.RequireUtcInstant(leftEndUtc.Value, nameof(leftEndUtc));

            if (rightEndUtc.HasValue)
                rightEndUtc = StrictTimeSerializer.RequireUtcInstant(rightEndUtc.Value, nameof(rightEndUtc));

            return (!rightEndUtc.HasValue || leftStartUtc < rightEndUtc.Value)
                && (!leftEndUtc.HasValue || rightStartUtc < leftEndUtc.Value);
        }

        private static bool HasInternalOverlap(List<ActivityModel> activities)
        {
            var ordered = activities
                .Select(a => GetActivityIntervalUtc(a, validateOrder: true))
                .OrderBy(a => a.StartUtc)
                .ToList();

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var current = ordered[i];
                var next = ordered[i + 1];

                if (ActivityIntervalsOverlap(current.StartUtc, current.EndUtc, next.StartUtc, next.EndUtc))
                    return true;
            }

            return false;
        }

        private sealed class ActivityRow
        {
            public int ActivityID { get; set; }
            public long CardID { get; set; }
            public string Start { get; set; } = "";
            public string? End { get; set; }
            public string ValueRateName { get; set; } = "";
            public double ValuePerMinute { get; set; }
        }

        private sealed class ToggleActivityRowResult
        {
            public ActivityRow? Closed { get; init; }
            public ActivityRow? Opened { get; init; }
        }

        private static class ActivityMapper
        {
            public static ActivityModel ToModel(ActivityRow row, Func<string, DateTime> parseIsoDateTime)
            {
                if (row == null)
                    throw new ArgumentNullException(nameof(row));

                if (string.IsNullOrWhiteSpace(row.Start))
                    throw new InvalidOperationException("ActivityRow.Start is required.");

                DateTime? end = null;
                if (!string.IsNullOrWhiteSpace(row.End))
                    end = parseIsoDateTime(row.End!);

                return new ActivityModel
                {
                    Id = row.ActivityID,
                    CardID = row.CardID,
                    StartDate = parseIsoDateTime(row.Start),
                    EndDate = end,
                    RateName = row.ValueRateName ?? "",
                    ValuePerMinute = row.ValuePerMinute
                };
            }
        }
    }
}
