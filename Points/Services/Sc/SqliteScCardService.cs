using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Sc;

public sealed class SqliteScCardService : IScCardService
{
    private readonly ISqliteConnectionContext _context;
    private readonly ITimeZoneService _timeZoneService;
    private readonly ICardScheduleService _cardScheduleService;

    public SqliteScCardService(
        ISqliteConnectionContext context,
        ITimeZoneService timeZoneService,
        ICardScheduleService cardScheduleService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _cardScheduleService = cardScheduleService ?? throw new ArgumentNullException(nameof(cardScheduleService));
    }

    public async Task<ScCardModel> GetScModelDataAsync(int id)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                s.ScCardID     AS ScCardID,
                s.CardID       AS CardID,
                c.DisplayOrder AS DisplayOrder,
                c.Title        AS Title,
                c.Tags         AS Tags,
                s.Status       AS Status,
                s.Description  AS Description
            FROM ScCard s
            JOIN Card c ON c.CardID = s.CardID
            WHERE s.ScCardID = ?
            LIMIT 1;";

        var row = (await _context.Db.QueryAsync<ScCardJoinedRow>(sql, id)).FirstOrDefault();
        if (row == null)
            throw new KeyNotFoundException($"ScCard not found. ScCardID={id}");

        var model = MapScRowToModel(row);

        await LoadActivityAsync(model);
        await LoadStepsAndRepsAsync(model);
        await LoadSchedulesAsync(model);

        return model;
    }

    public async Task<List<ScCardModel>> GetScModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                s.ScCardID     AS ScCardID,
                s.CardID       AS CardID,
                c.DisplayOrder AS DisplayOrder,
                c.Title        AS Title,
                c.Tags         AS Tags,
                s.Status       AS Status,
                s.Description  AS Description
            FROM ScCard s
            JOIN Card c ON c.CardID = s.CardID
            ORDER BY c.DisplayOrder, s.ScCardID;";

        var rows = await _context.Db.QueryAsync<ScCardJoinedRow>(sql);
        if (rows.Count == 0)
            return new List<ScCardModel>();

        var models = rows.Select(MapScRowToModel).ToList();
        var byScId = models.ToDictionary(m => m.Id);

        await LoadActivitiesForCardsAsync(rows, byScId, rangeStart, rangeEnd);
        await LoadStepsAndRepsForCardsAsync(rows, byScId, rangeStart, rangeEnd);

        foreach (var model in models)
        {
            await LoadSchedulesAsync(model);
        }

        return models;
    }

    public async Task SaveScModelDataAsync(ScCardModel model, long cardId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (cardId <= 0)
            throw new ArgumentException("SC cards must be attached to a saved base card.", nameof(cardId));

        await _context.InitializeAsync();

        model.CardID = cardId;

        if (model.Id == 0)
        {
            await _context.Db.ExecuteAsync(
                "INSERT INTO ScCard (CardID, Status, Description) VALUES (?, ?, ?);",
                cardId,
                model.Status,
                model.Description);

            model.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }
        else
        {
            await _context.Db.ExecuteAsync(
                "UPDATE ScCard SET Status = ?, Description = ? WHERE CardID = ?;",
                model.Status,
                model.Description,
                cardId);
        }

        foreach (var step in model.Steps)
        {
            if (step.Id == 0)
            {
                await _context.Db.ExecuteAsync(
                    "INSERT INTO ScCardStep (ScCardID, SortOrder, Title, StepValue) VALUES (?, ?, ?, ?);",
                    model.Id,
                    step.SortOrder,
                    step.Title,
                    step.StepValue);

                step.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await _context.Db.ExecuteAsync(
                    "UPDATE ScCardStep SET SortOrder = ?, Title = ?, StepValue = ? WHERE ScCardStepID = ?;",
                    step.SortOrder,
                    step.Title,
                    step.StepValue,
                    step.Id);
            }

            const string insertRepSql = @"
                INSERT OR IGNORE INTO ScCardStepRep (ScCardStepID, TimeStamp, StepValue)
                VALUES (?, ?, ?);";

            foreach (var rep in step.Reps)
            {
                await _context.Db.ExecuteAsync(
                    insertRepSql,
                    step.Id,
                    SerializeInstantForDb(rep),
                    step.StepValue);
            }
        }

        await _cardScheduleService.SaveCardSchedulesAsync(cardId, model.Schedules);
    }

    public async Task RemoveRepForStepAsync(int scCardStepId, DateTime repTime)
    {
        await _context.InitializeAsync();

        var rows = await _context.Db.QueryAsync<ScCardStepRepRow>(
            @"SELECT
                  ScCardStepID AS ScCardStepID,
                  TimeStamp    AS TimeStamp,
                  StepValue    AS StepValue
              FROM ScCardStepRep
              WHERE ScCardStepID = ?;",
            scCardStepId);

        var cutoffUtc = ToUtcInstantForWrite(repTime);
        var target = rows
            .Select(r => new { Row = r, Utc = ParseInstantUtc(r.TimeStamp) })
            .Where(x => x.Utc <= cutoffUtc)
            .OrderByDescending(x => x.Utc)
            .FirstOrDefault();

        if (target == null)
            return;

        await _context.Db.ExecuteAsync(
            @"DELETE FROM ScCardStepRep
              WHERE ScCardStepID = ?
                AND TimeStamp = ?;",
            scCardStepId,
            target.Row.TimeStamp);
    }

    private async Task LoadActivityAsync(ScCardModel model)
    {
        const string sql = @"
            SELECT
                ActivityID       AS ActivityID,
                CardID           AS CardID,
                Start            AS Start,
                ""End""          AS End,
                ValueRateName    AS ValueRateName,
                ValuePerMinute   AS ValuePerMinute
            FROM Activity
            WHERE CardID = ?
            ORDER BY Start;";

        var rows = await _context.Db.QueryAsync<ActivityRow>(sql, model.CardID);
        model.Activity = rows.Select(MapActivityRowToModel).ToList();
    }

    private async Task LoadActivitiesForCardsAsync(
        List<ScCardJoinedRow> rows,
        Dictionary<int, ScCardModel> byScId,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
        if (cardIds.Count == 0)
            return;

        var placeholders = string.Join(", ", cardIds.Select(_ => "?"));
        var sql = $@"
            SELECT
                ActivityID       AS ActivityID,
                CardID           AS CardID,
                Start            AS Start,
                ""End""          AS End,
                ValueRateName    AS ValueRateName,
                ValuePerMinute   AS ValuePerMinute
            FROM Activity
            WHERE CardID IN ({placeholders})
            ORDER BY CardID, Start;";

        var rangeUtc = ToActivityQueryUtcRange(rangeStart, rangeEnd);
        var activityRows = await _context.Db.QueryAsync<ActivityRow>(sql, cardIds.Cast<object>().ToArray());
        var activityByCardId = activityRows
            .Select(MapActivityRowToModel)
            .Where(a => ActivityOverlapsUtcRange(a, rangeUtc))
            .GroupBy(a => a.CardID)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var row in rows)
        {
            if (!byScId.TryGetValue(row.ScCardID, out var model))
                continue;

            model.Activity = activityByCardId.TryGetValue(row.CardID, out var activities)
                ? activities
                : new List<ActivityModel>();
        }
    }

    private async Task LoadStepsAndRepsAsync(ScCardModel model)
    {
        const string stepsSql = @"
            SELECT
                ScCardStepID AS ScCardStepID,
                ScCardID     AS ScCardID,
                SortOrder    AS SortOrder,
                Title        AS Title,
                StepValue    AS StepValue
            FROM ScCardStep
            WHERE ScCardID = ?
            ORDER BY SortOrder;";

        var stepRows = await _context.Db.QueryAsync<ScCardStepRow>(stepsSql, model.Id);

        const string repsSql = @"
            SELECT
                ScCardStepID AS ScCardStepID,
                TimeStamp    AS TimeStamp,
                StepValue    AS StepValue
            FROM ScCardStepRep
            WHERE ScCardStepID = ?
            ORDER BY TimeStamp;";

        foreach (var row in stepRows)
        {
            var step = MapStepRowToModel(row);
            var repRows = await _context.Db.QueryAsync<ScCardStepRepRow>(repsSql, step.Id);
            step.Reps = repRows
                .Select(r => ParseInstantUtc(r.TimeStamp))
                .ToList();

            model.Steps.Add(step);
        }
    }

    private async Task LoadStepsAndRepsForCardsAsync(
        List<ScCardJoinedRow> rows,
        Dictionary<int, ScCardModel> byScId,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var scIds = rows.Select(r => r.ScCardID).Distinct().ToList();
        if (scIds.Count == 0)
            return;

        var scPlaceholders = string.Join(", ", scIds.Select(_ => "?"));
        var stepsSql = $@"
            SELECT
                ScCardStepID AS ScCardStepID,
                ScCardID     AS ScCardID,
                SortOrder    AS SortOrder,
                Title        AS Title,
                StepValue    AS StepValue
            FROM ScCardStep
            WHERE ScCardID IN ({scPlaceholders})
            ORDER BY ScCardID, SortOrder;";

        var stepRows = await _context.Db.QueryAsync<ScCardStepRow>(stepsSql, scIds.Cast<object>().ToArray());
        var stepIdToStep = new Dictionary<int, ScStepModel>();
        var stepIds = new List<int>();

        foreach (var row in stepRows)
        {
            if (!byScId.TryGetValue(row.ScCardID, out var parent))
                continue;

            var step = MapStepRowToModel(row);
            parent.Steps.Add(step);
            stepIdToStep[step.Id] = step;
            stepIds.Add(step.Id);
        }

        if (stepIds.Count == 0)
            return;

        var stepPlaceholders = string.Join(", ", stepIds.Select(_ => "?"));
        var repsSql = $@"
            SELECT
                ScCardStepID AS ScCardStepID,
                TimeStamp    AS TimeStamp,
                StepValue    AS StepValue
            FROM ScCardStepRep
            WHERE ScCardStepID IN ({stepPlaceholders})
            ORDER BY ScCardStepID, TimeStamp;";

        var rangeUtc = ToInstantQueryUtcRange(rangeStart, rangeEnd);
        var repRows = await _context.Db.QueryAsync<ScCardStepRepRow>(repsSql, stepIds.Cast<object>().ToArray());

        foreach (var row in repRows)
        {
            if (!stepIdToStep.TryGetValue(row.ScCardStepID, out var step))
                continue;

            var repUtc = ParseInstantUtc(row.TimeStamp);
            if (InstantFallsInUtcRange(repUtc, rangeUtc))
                step.Reps.Add(repUtc);
        }
    }

    private async Task LoadSchedulesAsync(ScCardModel model)
    {
        var schedules = await _cardScheduleService.GetCardSchedulesForCardAsync(model.CardID);
        model.SetSchedules(schedules);
    }

    private static ScCardModel MapScRowToModel(ScCardJoinedRow row)
    {
        return new ScCardModel
        {
            Id = row.ScCardID,
            CardID = row.CardID,
            DisplayOrder = row.DisplayOrder,
            Title = row.Title ?? string.Empty,
            Tags = row.Tags ?? string.Empty,
            Status = row.Status ?? string.Empty,
            Description = row.Description ?? string.Empty,
            Activity = new List<ActivityModel>()
        };
    }

    private static ScStepModel MapStepRowToModel(ScCardStepRow row)
    {
        return new ScStepModel
        {
            Id = row.ScCardStepID,
            SortOrder = row.SortOrder,
            Title = row.Title ?? string.Empty,
            StepValue = row.StepValue,
            Reps = new List<DateTime>()
        };
    }

    private ActivityModel MapActivityRowToModel(ActivityRow row)
    {
        if (row == null)
            throw new ArgumentNullException(nameof(row));

        if (string.IsNullOrWhiteSpace(row.Start))
            throw new InvalidOperationException("ActivityRow.Start is required.");

        DateTime? end = null;
        if (!string.IsNullOrWhiteSpace(row.End))
            end = ParseInstantUtc(row.End!);

        return new ActivityModel
        {
            Id = row.ActivityID,
            CardID = row.CardID,
            StartDate = ParseInstantUtc(row.Start),
            EndDate = end,
            RateName = row.ValueRateName ?? string.Empty,
            ValuePerMinute = row.ValuePerMinute
        };
    }

    private UtcDateTimeRange ToInstantQueryUtcRange(DateTime rangeStart, DateTime rangeEnd)
    {
        return new UtcDateTimeRange(
            ToUtcInstantForWrite(rangeStart),
            ToUtcInstantForWrite(rangeEnd));
    }

    private UtcDateTimeRange ToActivityQueryUtcRange(DateTime rangeStart, DateTime rangeEnd)
    {
        return ToInstantQueryUtcRange(rangeStart, rangeEnd);
    }

    private static (DateTime StartUtc, DateTime? EndUtc) GetActivityIntervalUtc(ActivityModel activity)
    {
        if (activity == null)
            throw new ArgumentNullException(nameof(activity));

        var startUtc = StrictTimeSerializer.RequireUtcInstant(activity.StartDate, nameof(activity.StartDate));
        var endUtc = activity.EndDate.HasValue
            ? StrictTimeSerializer.RequireUtcInstant(activity.EndDate.Value, nameof(activity.EndDate))
            : (DateTime?)null;

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

    private static bool ActivityOverlapsUtcRange(ActivityModel activity, UtcDateTimeRange range)
    {
        var (startUtc, endUtc) = GetActivityIntervalUtc(activity);
        return ActivityIntervalsOverlap(startUtc, endUtc, range.StartUtc, range.EndUtc);
    }

    private static bool InstantFallsInUtcRange(DateTime utcInstant, UtcDateTimeRange range)
    {
        utcInstant = StrictTimeSerializer.RequireUtcInstant(utcInstant, nameof(utcInstant));
        return utcInstant >= range.StartUtc && utcInstant <= range.EndUtc;
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

    private sealed class ScCardJoinedRow
    {
        public int ScCardID { get; set; }
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
    }

    private sealed class ScCardStepRow
    {
        public int ScCardStepID { get; set; }
        public int ScCardID { get; set; }
        public int SortOrder { get; set; }
        public string? Title { get; set; }
        public double StepValue { get; set; }
    }

    private sealed class ScCardStepRepRow
    {
        public int ScCardStepID { get; set; }
        public string TimeStamp { get; set; } = "";
        public double StepValue { get; set; }
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
}
