using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Sqlite.Queries;
using Points.Services.Time;

namespace Points.Services.Tat;

public sealed class SqliteTatCardService : ITatCardService
{
    private readonly ISqliteConnectionContext _context;
    private readonly ITimeZoneService _timeZoneService;
    private readonly ICardScheduleService _cardScheduleService;

    public SqliteTatCardService(
        ISqliteConnectionContext context,
        ITimeZoneService timeZoneService,
        ICardScheduleService cardScheduleService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _cardScheduleService = cardScheduleService ?? throw new ArgumentNullException(nameof(cardScheduleService));
    }

    public async Task<TatCardModel> GetTatModelDataAsync(int id)
    {
        await _context.InitializeAsync();

        var row = (await _context.Db.QueryAsync<TatCardJoinedRow>(TatSql.GetTatModelDataById, id)).FirstOrDefault();
        if (row == null)
            throw new KeyNotFoundException($"TatCard not found. TatCardID={id}");

        var model = MapTatRowToModel(row);

        await LoadActivityAsync(model);
        await LoadValueRatesAsync(model);
        await LoadSchedulesAsync(model);

        return model;
    }

    public async Task<List<TatCardModel>> GetTatModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                t.TatCardID                 AS TatCardID,
                t.CardID                    AS CardID,
                c.DisplayOrder              AS DisplayOrder,
                c.Title                     AS Title,
                c.Tags                      AS Tags,
                t.ValuePerMinute            AS ValuePerMinute,
                t.Status                    AS Status,
                t.Description               AS Description,
                t.TargetActiveTimeSeconds   AS TargetActiveTimeSeconds
            FROM TatCard t
            JOIN Card c ON c.CardID = t.CardID
            ORDER BY c.DisplayOrder, t.TatCardID;";

        var rows = await _context.Db.QueryAsync<TatCardJoinedRow>(sql);
        if (rows.Count == 0)
            return new List<TatCardModel>();

        var models = rows.Select(MapTatRowToModel).ToList();
        var byTatId = models.ToDictionary(m => m.Id);

        await LoadActivitiesForCardsAsync(rows, byTatId, rangeStart, rangeEnd);
        await LoadValueRatesForCardsAsync(rows, byTatId);

        foreach (var model in models)
        {
            await LoadSchedulesAsync(model);
        }

        return models;
    }

    public async Task SaveTatModelDataAsync(TatCardModel model, long cardId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (cardId <= 0)
            throw new ArgumentException("TAT cards must be attached to a saved base card.", nameof(cardId));

        await _context.InitializeAsync();

        model.CardID = cardId;

        if (model.Id == 0)
        {
            await _context.Db.ExecuteAsync(
                @"INSERT INTO TatCard (CardID, ValuePerMinute, Status, Description, TargetActiveTimeSeconds)
                  VALUES (?, ?, ?, ?, ?);",
                cardId,
                model.ValuePerMinute,
                model.Status,
                model.Description,
                ToTargetActiveTimeSeconds(model.TargetActiveTime));

            model.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }
        else
        {
            await _context.Db.ExecuteAsync(
                @"UPDATE TatCard
                  SET ValuePerMinute = ?,
                      Status = ?,
                      Description = ?,
                      TargetActiveTimeSeconds = ?
                  WHERE CardID = ?;",
                model.ValuePerMinute,
                model.Status,
                model.Description,
                ToTargetActiveTimeSeconds(model.TargetActiveTime),
                cardId);
        }

        await SyncValueRatesAsync(model);
        await _cardScheduleService.SaveCardSchedulesAsync(cardId, model.Schedules);
    }

    private async Task LoadActivityAsync(TatCardModel model)
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
        List<TatCardJoinedRow> rows,
        Dictionary<int, TatCardModel> byTatId,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var cardIds = rows.Select(r => r.CardID).Distinct().ToList();
        if (cardIds.Count == 0)
            return;

        var placeholders = string.Join(",", cardIds.Select(_ => "?"));
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
            if (!byTatId.TryGetValue(row.TatCardID, out var model))
                continue;

            model.Activity = activityByCardId.TryGetValue(row.CardID, out var activities)
                ? activities
                : new List<ActivityModel>();
        }
    }

    private async Task LoadValueRatesAsync(TatCardModel model)
    {
        const string sql = @"
            SELECT
                TatCardValueRateID AS TatCardValueRateID,
                TatCardID          AS TatCardID,
                RateName           AS RateName,
                ValuePerMinute     AS ValuePerMinute
            FROM TatCardValueRate
            WHERE TatCardID = ?
            ORDER BY TatCardValueRateID;";

        var rows = await _context.Db.QueryAsync<TatValueRateRow>(sql, model.Id);
        model.ValueRates = rows.Select(MapValueRateRowToModel).ToList();
    }

    private async Task LoadValueRatesForCardsAsync(
        List<TatCardJoinedRow> rows,
        Dictionary<int, TatCardModel> byTatId)
    {
        var tatIds = rows.Select(r => r.TatCardID).Distinct().ToList();
        if (tatIds.Count == 0)
            return;

        var placeholders = string.Join(",", tatIds.Select(_ => "?"));
        var sql = $@"
            SELECT
                TatCardValueRateID AS TatCardValueRateID,
                TatCardID          AS TatCardID,
                RateName           AS RateName,
                ValuePerMinute     AS ValuePerMinute
            FROM TatCardValueRate
            WHERE TatCardID IN ({placeholders})
            ORDER BY TatCardID, TatCardValueRateID;";

        var valueRateRows = await _context.Db.QueryAsync<TatValueRateRow>(sql, tatIds.Cast<object>().ToArray());

        foreach (var row in valueRateRows)
        {
            if (!byTatId.TryGetValue(row.TatCardID, out var model))
                continue;

            model.ValueRates.Add(MapValueRateRowToModel(row));
        }
    }

    private async Task LoadSchedulesAsync(TatCardModel model)
    {
        var schedules = await _cardScheduleService.GetCardSchedulesForCardAsync(model.CardID);
        model.SetSchedules(schedules);
    }

    private async Task SyncValueRatesAsync(TatCardModel model)
    {
        var existingValueRates = await _context.Db.QueryAsync<TatValueRateRow>(
            @"SELECT
                  TatCardValueRateID AS TatCardValueRateID,
                  TatCardID          AS TatCardID,
                  RateName           AS RateName,
                  ValuePerMinute     AS ValuePerMinute
              FROM TatCardValueRate
              WHERE TatCardID = ?;",
            model.Id);

        var remaining = existingValueRates.ToList();

        foreach (var valueRate in model.ValueRates)
        {
            if (valueRate.Id == 0)
            {
                await _context.Db.ExecuteAsync(
                    @"INSERT INTO TatCardValueRate (TatCardID, RateName, ValuePerMinute)
                      VALUES (?, ?, ?);",
                    model.Id,
                    valueRate.RateName,
                    valueRate.ValuePerMinute);

                valueRate.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                continue;
            }

            await _context.Db.ExecuteAsync(
                @"UPDATE TatCardValueRate
                  SET TatCardID = ?,
                      RateName = ?,
                      ValuePerMinute = ?
                  WHERE TatCardValueRateID = ?;",
                model.Id,
                valueRate.RateName,
                valueRate.ValuePerMinute,
                valueRate.Id);

            var retained = remaining.FirstOrDefault(x => x.TatCardValueRateID == valueRate.Id);
            if (retained != null)
                remaining.Remove(retained);
        }

        foreach (var valueRateToDelete in remaining)
        {
            await _context.Db.ExecuteAsync(
                "DELETE FROM TatCardValueRate WHERE TatCardValueRateID = ?;",
                valueRateToDelete.TatCardValueRateID);
        }
    }

    private TatCardModel MapTatRowToModel(TatCardJoinedRow row)
    {
        return new TatCardModel
        {
            Id = row.TatCardID,
            CardID = row.CardID,
            DisplayOrder = row.DisplayOrder,
            Title = row.Title ?? string.Empty,
            Tags = row.Tags ?? string.Empty,
            ValuePerMinute = row.ValuePerMinute,
            Status = row.Status ?? string.Empty,
            Description = row.Description ?? string.Empty,
            TargetActiveTime = row.TargetActiveTimeSeconds == null
                ? null
                : TimeSpan.FromSeconds(row.TargetActiveTimeSeconds.Value),
            Activity = new List<ActivityModel>(),
            ValueRates = new List<ValueRateModel>()
        };
    }

    private static ValueRateModel MapValueRateRowToModel(TatValueRateRow row)
    {
        return new ValueRateModel
        {
            Id = row.TatCardValueRateID,
            RateName = row.RateName ?? string.Empty,
            ValuePerMinute = row.ValuePerMinute
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

    private UtcDateTimeRange ToActivityQueryUtcRange(DateTime rangeStart, DateTime rangeEnd)
    {
        return new UtcDateTimeRange(
            ToUtcInstantForWrite(rangeStart),
            ToUtcInstantForWrite(rangeEnd));
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

    private static double? ToTargetActiveTimeSeconds(TimeSpan? value)
    {
        return value.HasValue ? value.Value.TotalSeconds : null;
    }

    private sealed class TatCardJoinedRow
    {
        public int TatCardID { get; set; }
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public double ValuePerMinute { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public int? TargetActiveTimeSeconds { get; set; }
    }

    private sealed class TatValueRateRow
    {
        public int TatCardValueRateID { get; set; }
        public int TatCardID { get; set; }
        public string? RateName { get; set; }
        public double ValuePerMinute { get; set; }
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
