using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Trackers;

public sealed class SqliteTrackerService : ITrackerService
{
    private readonly ISqliteConnectionContext _context;
    private readonly ITimeZoneService _timeZoneService;
    private readonly ICardScheduleService _cardScheduleService;

    public SqliteTrackerService(
        ISqliteConnectionContext context,
        ITimeZoneService timeZoneService,
        ICardScheduleService cardScheduleService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _cardScheduleService = cardScheduleService ?? throw new ArgumentNullException(nameof(cardScheduleService));
    }

    public async Task<ValueTrackerCardModel> GetValueTrackerCardModelDataAsync(int id)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                vt.ValueTrackerCardID AS ValueTrackerCardID,
                vt.CardID             AS CardID,
                c.DisplayOrder        AS DisplayOrder,
                c.Title               AS Title,
                c.Tags                AS Tags,
                vt.Status             AS Status,
                vt.Unit               AS Unit,
                vt.CreatedDate        AS CreatedDate,
                vt.RangeStart         AS RangeStart,
                vt.ScheduleEvery      AS ScheduleEvery,
                vt.ScheduleUnit       AS ScheduleUnit
            FROM ValueTrackerCard vt
            JOIN Card c ON c.CardID = vt.CardID
            WHERE vt.ValueTrackerCardID = ?
            LIMIT 1;";

        var row = (await _context.Db.QueryAsync<ValueTrackerJoinedRow>(sql, id)).FirstOrDefault();
        if (row == null)
            throw new KeyNotFoundException($"ValueTrackerCard not found. ValueTrackerCardID={id}");

        var model = MapValueTrackerRowToModel(row);
        await LoadTrackerValuesAsync(model);
        await LoadValueTrackerSchedulesAsync(model);

        return model;
    }

    public async Task<List<ValueTrackerCardModel>> GetValueTrackerCardModelsDataAsync(string? whereClause = null)
    {
        await _context.InitializeAsync();

        var sql = @"
            SELECT
                vt.ValueTrackerCardID AS ValueTrackerCardID,
                vt.CardID             AS CardID,
                c.DisplayOrder        AS DisplayOrder,
                c.Title               AS Title,
                c.Tags                AS Tags,
                vt.Status             AS Status,
                vt.Unit               AS Unit,
                vt.CreatedDate        AS CreatedDate,
                vt.RangeStart         AS RangeStart,
                vt.ScheduleEvery      AS ScheduleEvery,
                vt.ScheduleUnit       AS ScheduleUnit
            FROM ValueTrackerCard vt
            JOIN Card c ON c.CardID = vt.CardID";

        var hasCustomOrdering = HasCustomOrderingOrLimit(whereClause);
        sql = AppendWhereClause(sql, whereClause);
        if (!hasCustomOrdering)
            sql += " ORDER BY c.DisplayOrder, vt.ValueTrackerCardID;";

        var rows = await _context.Db.QueryAsync<ValueTrackerJoinedRow>(sql);
        if (rows.Count == 0)
            return new List<ValueTrackerCardModel>();

        var models = rows.Select(MapValueTrackerRowToModel).ToList();

        foreach (var model in models)
        {
            await LoadValueTrackerSchedulesAsync(model);
        }

        await LoadTrackerValuesForCardsAsync(models.Cast<TrackerCardModel>().ToList());

        return models;
    }

    public async Task<EventTrackerCardModel> GetEventTrackerCardModelDataAsync(int id)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                et.EventTrackerCardID AS EventTrackerCardID,
                et.CardID             AS CardID,
                c.DisplayOrder        AS DisplayOrder,
                c.Title               AS Title,
                c.Tags                AS Tags,
                et.Status             AS Status,
                et.Unit               AS Unit,
                et.CreatedDate        AS CreatedDate,
                et.RangeStart         AS RangeStart,
                et.GroupByPeriod      AS GroupByPeriod
            FROM EventTrackerCard et
            JOIN Card c ON c.CardID = et.CardID
            WHERE et.EventTrackerCardID = ?
            LIMIT 1;";

        var row = (await _context.Db.QueryAsync<EventTrackerJoinedRow>(sql, id)).FirstOrDefault();
        if (row == null)
            throw new KeyNotFoundException($"EventTrackerCard not found. EventTrackerCardID={id}");

        var model = MapEventTrackerRowToModel(row);
        await LoadTrackerValuesAsync(model);

        return model;
    }

    public async Task<List<EventTrackerCardModel>> GetEventTrackerCardModelsDataAsync(string? whereClause = null)
    {
        await _context.InitializeAsync();

        var sql = @"
            SELECT
                et.EventTrackerCardID AS EventTrackerCardID,
                et.CardID             AS CardID,
                c.DisplayOrder        AS DisplayOrder,
                c.Title               AS Title,
                c.Tags                AS Tags,
                et.Status             AS Status,
                et.Unit               AS Unit,
                et.CreatedDate        AS CreatedDate,
                et.RangeStart         AS RangeStart,
                et.GroupByPeriod      AS GroupByPeriod
            FROM EventTrackerCard et
            JOIN Card c ON c.CardID = et.CardID";

        var hasCustomOrdering = HasCustomOrderingOrLimit(whereClause);
        sql = AppendWhereClause(sql, whereClause);
        if (!hasCustomOrdering)
            sql += " ORDER BY c.DisplayOrder, et.EventTrackerCardID;";

        var rows = await _context.Db.QueryAsync<EventTrackerJoinedRow>(sql);
        if (rows.Count == 0)
            return new List<EventTrackerCardModel>();

        var models = rows.Select(MapEventTrackerRowToModel).ToList();
        await LoadTrackerValuesForCardsAsync(models.Cast<TrackerCardModel>().ToList());

        return models;
    }

    public async Task SaveValueTrackerCardModelDataAsync(ValueTrackerCardModel model, long cardId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (cardId <= 0)
            throw new ArgumentException("Value trackers must be attached to a saved base card.", nameof(cardId));

        await _context.InitializeAsync();

        model.CardID = cardId;

        if (model.Id == 0)
        {
            await _context.Db.ExecuteAsync(
                @"INSERT INTO ValueTrackerCard
                    (CardID, Status, Unit, CreatedDate, RangeStart, ScheduleEvery, ScheduleUnit)
                  VALUES (?, ?, ?, ?, ?, ?, ?);",
                cardId,
                model.Status,
                model.Unit ?? string.Empty,
                SerializeLocalDateTimeForDb(model.CreatedDate),
                SerializeLocalDateTimeForDb(model.RangeStart),
                model.ScheduleEvery,
                model.ScheduleUnit ?? "Week");

            model.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }
        else
        {
            await _context.Db.ExecuteAsync(
                @"UPDATE ValueTrackerCard
                  SET Unit = ?,
                      Status = ?,
                      CreatedDate = ?,
                      RangeStart = ?,
                      ScheduleEvery = ?,
                      ScheduleUnit = ?
                  WHERE CardID = ?;",
                model.Unit ?? string.Empty,
                model.Status,
                SerializeLocalDateTimeForDb(model.CreatedDate),
                SerializeLocalDateTimeForDb(model.RangeStart),
                model.ScheduleEvery,
                model.ScheduleUnit ?? "Week",
                cardId);
        }

        await _cardScheduleService.SaveCardSchedulesAsync(cardId, model.Schedules);
        await SaveTrackerValuesAsync(cardId, model.Values);
    }

    public async Task SaveEventTrackerCardModelDataAsync(EventTrackerCardModel model, long cardId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (cardId <= 0)
            throw new ArgumentException("Event trackers must be attached to a saved base card.", nameof(cardId));

        await _context.InitializeAsync();

        model.CardID = cardId;

        if (model.Id == 0)
        {
            await _context.Db.ExecuteAsync(
                @"INSERT INTO EventTrackerCard
                    (CardID, Status, Unit, CreatedDate, RangeStart, GroupByPeriod)
                  VALUES (?, ?, ?, ?, ?, ?);",
                cardId,
                model.Status,
                model.Unit ?? string.Empty,
                SerializeLocalDateTimeForDb(model.CreatedDate),
                SerializeLocalDateTimeForDb(model.RangeStart),
                model.GroupByPeriod ?? "Day");

            model.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }
        else
        {
            await _context.Db.ExecuteAsync(
                @"UPDATE EventTrackerCard
                  SET Unit = ?,
                      Status = ?,
                      CreatedDate = ?,
                      RangeStart = ?,
                      GroupByPeriod = ?
                  WHERE CardID = ?;",
                model.Unit ?? string.Empty,
                model.Status,
                SerializeLocalDateTimeForDb(model.CreatedDate),
                SerializeLocalDateTimeForDb(model.RangeStart),
                model.GroupByPeriod ?? "Day",
                cardId);
        }

        await SaveTrackerValuesAsync(cardId, model.Values);
    }

    private async Task LoadValueTrackerSchedulesAsync(ValueTrackerCardModel model)
    {
        var schedules = await _cardScheduleService.GetCardSchedulesForCardAsync(model.CardID);
        model.SetSchedules(schedules);
    }

    private async Task LoadTrackerValuesAsync(TrackerCardModel model)
    {
        const string sql = @"
            SELECT
                TrackerValueID AS TrackerValueID,
                CardID         AS CardID,
                TimeStamp      AS TimeStamp,
                Value          AS Value
            FROM TrackerValue
            WHERE CardID = ?
            ORDER BY TimeStamp;";

        var rows = await _context.Db.QueryAsync<TrackerValueRow>(sql, model.CardID);
        model.SetValues(rows.Select(MapTrackerValueRowToModel).ToList());
    }

    private async Task LoadTrackerValuesForCardsAsync(List<TrackerCardModel> models)
    {
        if (models.Count == 0)
            return;

        var cardIds = models.Select(m => m.CardID).Distinct().ToList();
        var byCardId = models.ToDictionary(m => m.CardID);
        var placeholders = string.Join(", ", cardIds.Select(_ => "?"));

        var sql = $@"
            SELECT
                TrackerValueID AS TrackerValueID,
                CardID         AS CardID,
                TimeStamp      AS TimeStamp,
                Value          AS Value
            FROM TrackerValue
            WHERE CardID IN ({placeholders})
            ORDER BY CardID, TimeStamp;";

        var rows = await _context.Db.QueryAsync<TrackerValueRow>(sql, cardIds.Cast<object>().ToArray());

        foreach (var row in rows)
        {
            if (!byCardId.TryGetValue(row.CardID, out var parent))
                continue;

            parent.Values.Add(MapTrackerValueRowToModel(row));
        }
    }

    private async Task SaveTrackerValuesAsync(long cardId, IEnumerable<TrackerValueModel> values)
    {
        var existing = await _context.Db.QueryAsync<TrackerValueRow>(
            @"SELECT
                  TrackerValueID AS TrackerValueID,
                  CardID         AS CardID,
                  TimeStamp      AS TimeStamp,
                  Value          AS Value
              FROM TrackerValue
              WHERE CardID = ?;",
            cardId);

        var remaining = existing.ToList();

        foreach (var value in values)
        {
            if (value.Id == 0)
            {
                await _context.Db.ExecuteAsync(
                    @"INSERT INTO TrackerValue (CardID, TimeStamp, Value)
                      VALUES (?, ?, ?);",
                    cardId,
                    SerializeInstantForDb(value.Timestamp),
                    value.Value);

                value.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                continue;
            }

            await _context.Db.ExecuteAsync(
                @"UPDATE TrackerValue
                  SET TimeStamp = ?,
                      Value = ?
                  WHERE TrackerValueID = ?;",
                SerializeInstantForDb(value.Timestamp),
                value.Value,
                value.Id);

            var retained = remaining.FirstOrDefault(x => x.TrackerValueID == value.Id);
            if (retained != null)
                remaining.Remove(retained);
        }

        foreach (var valueToDelete in remaining)
        {
            await _context.Db.ExecuteAsync(
                "DELETE FROM UdmdTrans WHERE RelatedEntityType = ? AND RelatedEntityId = ?;",
                UdmdRelatedEntityTypes.TrackerValue,
                valueToDelete.TrackerValueID);

            await _context.Db.ExecuteAsync(
                "DELETE FROM TrackerValue WHERE TrackerValueID = ?;",
                valueToDelete.TrackerValueID);
        }
    }

    private ValueTrackerCardModel MapValueTrackerRowToModel(ValueTrackerJoinedRow row)
    {
        return new ValueTrackerCardModel
        {
            Id = row.ValueTrackerCardID,
            CardID = row.CardID,
            DisplayOrder = row.DisplayOrder,
            Title = row.Title ?? string.Empty,
            Tags = row.Tags ?? string.Empty,
            Status = row.Status ?? string.Empty,
            Unit = row.Unit ?? string.Empty,
            CreatedDate = ParseLocalDateTime(row.CreatedDate),
            RangeStart = ParseLocalDateTime(row.RangeStart),
            ScheduleEvery = row.ScheduleEvery,
            ScheduleUnit = row.ScheduleUnit ?? "Week"
        };
    }

    private EventTrackerCardModel MapEventTrackerRowToModel(EventTrackerJoinedRow row)
    {
        return new EventTrackerCardModel
        {
            Id = row.EventTrackerCardID,
            CardID = row.CardID,
            DisplayOrder = row.DisplayOrder,
            Title = row.Title ?? string.Empty,
            Tags = row.Tags ?? string.Empty,
            Status = row.Status ?? string.Empty,
            Unit = row.Unit ?? string.Empty,
            CreatedDate = ParseLocalDateTime(row.CreatedDate),
            RangeStart = ParseLocalDateTime(row.RangeStart),
            GroupByPeriod = row.GroupByPeriod ?? "Day"
        };
    }

    private TrackerValueModel MapTrackerValueRowToModel(TrackerValueRow row)
    {
        return new TrackerValueModel
        {
            Id = row.TrackerValueID,
            Timestamp = ParseInstantUtc(row.TimeStamp),
            Value = row.Value
        };
    }

    private static string AppendWhereClause(string sql, string? whereClause)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            return sql;

        var wc = whereClause.Trim();

        if (wc.StartsWith("WHERE", StringComparison.OrdinalIgnoreCase) ||
            wc.StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase) ||
            wc.StartsWith("LIMIT", StringComparison.OrdinalIgnoreCase))
        {
            return sql + " " + wc;
        }

        return sql + " WHERE " + wc;
    }

    private static bool HasCustomOrderingOrLimit(string? whereClause)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            return false;

        var wc = whereClause.Trim();
        return wc.StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase) ||
               wc.StartsWith("LIMIT", StringComparison.OrdinalIgnoreCase) ||
               wc.Contains(" ORDER BY ", StringComparison.OrdinalIgnoreCase) ||
               wc.Contains(" LIMIT ", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ParseLocalDateTime(string value)
    {
        return LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime;
    }

    private DateTime ParseInstantUtc(string value)
    {
        return LegacyTimeReader.ReadInstantUtc(value, _timeZoneService).UtcInstant;
    }

    private static string SerializeLocalDateTimeForDb(DateTime value)
    {
        return StrictTimeSerializer.SerializeLocalDateTime(value);
    }

    private string SerializeInstantForDb(DateTime value)
    {
        return StrictTimeSerializer.SerializeUtcInstant(ToUtcInstantForWrite(value));
    }

    private DateTime ToUtcInstantForWrite(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? StrictTimeSerializer.RequireUtcInstant(value, nameof(value))
            : _timeZoneService.ToUtcFromLocal(value);
    }

    private sealed class TrackerValueRow
    {
        public int TrackerValueID { get; set; }
        public long CardID { get; set; }
        public string TimeStamp { get; set; } = "";
        public double Value { get; set; }
    }

    private sealed class ValueTrackerJoinedRow
    {
        public int ValueTrackerCardID { get; set; }
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public string? Status { get; set; }
        public string? Unit { get; set; }
        public string CreatedDate { get; set; } = "";
        public string RangeStart { get; set; } = "";
        public int ScheduleEvery { get; set; }
        public string? ScheduleUnit { get; set; }
    }

    private sealed class EventTrackerJoinedRow
    {
        public int EventTrackerCardID { get; set; }
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public string? Status { get; set; }
        public string? Unit { get; set; }
        public string CreatedDate { get; set; } = "";
        public string RangeStart { get; set; } = "";
        public string? GroupByPeriod { get; set; }
    }
}
