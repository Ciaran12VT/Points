using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Missions;

public sealed class SqliteMissionCardService : IMissionCardService
{
    private readonly ISqliteConnectionContext _context;
    private readonly ITimeZoneService _timeZoneService;

    public SqliteMissionCardService(ISqliteConnectionContext context, ITimeZoneService timeZoneService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
    }

    public async Task<MissionCardModel> GetMissionCardModelDataAsync(int id)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                m.MissionCardID         AS MissionCardID,
                m.CardID                AS CardID,
                c.Title                 AS Title,
                c.Tags                  AS Tags,
                m.Status                AS Status,
                m.Description           AS Description,
                m.SubType               AS SubType,
                m.Value                 AS Value,
                m.CreatedDate           AS CreatedDate,
                m.AvailableFromDate     AS AvailableFromDate,
                m.DueDate               AS DueDate,
                m.CompletedDate         AS CompletedDate,
                m.EventDate             AS EventDate,
                m.EstCompletionTimeText AS EstCompletionTimeText,
                m.IsFailed              AS IsFailed,
                m.ValuePerMinute        AS ValuePerMinute
            FROM MissionCard m
            JOIN Card c ON c.CardID = m.CardID
            WHERE m.MissionCardID = ?
            LIMIT 1;";

        var row = (await _context.Db.QueryAsync<MissionCardJoinedRow>(sql, id)).FirstOrDefault();
        if (row == null)
            throw new KeyNotFoundException($"MissionCard not found. MissionCardID={id}");

        var model = MapMissionRowToModel(row);
        await LoadActivityAsync(model);

        return model;
    }

    public async Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null)
    {
        await _context.InitializeAsync();

        var sql = @"
            SELECT
                m.MissionCardID         AS MissionCardID,
                m.CardID                AS CardID,
                c.Title                 AS Title,
                c.Tags                  AS Tags,
                m.Status                AS Status,
                m.Description           AS Description,
                m.SubType               AS SubType,
                m.Value                 AS Value,
                m.CreatedDate           AS CreatedDate,
                m.AvailableFromDate     AS AvailableFromDate,
                m.DueDate               AS DueDate,
                m.CompletedDate         AS CompletedDate,
                m.EventDate             AS EventDate,
                m.EstCompletionTimeText AS EstCompletionTimeText,
                m.IsFailed              AS IsFailed,
                m.ValuePerMinute        AS ValuePerMinute
            FROM MissionCard m
            JOIN Card c ON c.CardID = m.CardID";

        sql = AppendWhereClause(sql, whereClause);

        var rows = await _context.Db.QueryAsync<MissionCardJoinedRow>(sql);
        if (rows.Count == 0)
            return new List<MissionCardModel>();

        var models = rows.Select(MapMissionRowToModel).ToList();
        var byCardId = models.ToDictionary(model => model.CardID);

        await LoadActivitiesForCardsAsync(byCardId);

        return rows
            .Select(row => byCardId[row.CardID])
            .ToList();
    }

    public async Task SaveMissionCardModelDataAsync(MissionCardModel model, long cardId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (cardId <= 0)
            throw new ArgumentException("Mission cards must be attached to a saved base card.", nameof(cardId));

        await _context.InitializeAsync();

        model.CardID = cardId;

        var createdDateText = SerializeInstantForDb(model.CreatedDate);
        var availableFromText = StrictTimeSerializer.SerializeLocalDateTime(model.AvailableFromDate);
        var dueDateText = StrictTimeSerializer.SerializeLocalDateTime(model.DueDate);
        var completedDateText = SerializeNullableInstantForDb(model.CompletedDate);
        var eventDateText = StrictTimeSerializer.SerializeNullableLocalDateTime(model.EventDate);
        var estCompletionTimeText = model.EstCompletionTimeText ?? string.Empty;

        if (model.Id == 0)
        {
            await _context.Db.ExecuteAsync(
                @"INSERT INTO MissionCard
                  (CardID,
                   Status,
                   Description,
                   SubType,
                   Value,
                   CreatedDate,
                   AvailableFromDate,
                   DueDate,
                   CompletedDate,
                   EventDate,
                   EstCompletionTimeText,
                   IsFailed,
                   ValuePerMinute)
                  VALUES
                  (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                cardId,
                model.Status ?? string.Empty,
                model.Description ?? string.Empty,
                model.SubType.ToString(),
                model.Value,
                createdDateText,
                availableFromText,
                dueDateText,
                completedDateText,
                eventDateText,
                estCompletionTimeText,
                model.IsFailed ? 1 : 0,
                model.ValuePerMinute);

            model.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            return;
        }

        await _context.Db.ExecuteAsync(
            @"UPDATE MissionCard
              SET Status                 = ?,
                  Description            = ?,
                  SubType                = ?,
                  Value                  = ?,
                  AvailableFromDate      = ?,
                  DueDate                = ?,
                  CompletedDate          = ?,
                  EventDate              = ?,
                  EstCompletionTimeText  = ?,
                  IsFailed               = ?,
                  ValuePerMinute         = ?
              WHERE CardID = ?;",
            model.Status ?? string.Empty,
            model.Description ?? string.Empty,
            model.SubType.ToString(),
            model.Value,
            availableFromText,
            dueDateText,
            completedDateText,
            eventDateText,
            estCompletionTimeText,
            model.IsFailed ? 1 : 0,
            model.ValuePerMinute,
            cardId);
    }

    private async Task LoadActivityAsync(MissionCardModel model)
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

    private async Task LoadActivitiesForCardsAsync(Dictionary<long, MissionCardModel> byCardId)
    {
        if (byCardId.Count == 0)
            return;

        var cardIds = byCardId.Keys.ToList();
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

        var rows = await _context.Db.QueryAsync<ActivityRow>(sql, cardIds.Cast<object>().ToArray());

        foreach (var row in rows)
        {
            if (!byCardId.TryGetValue(row.CardID, out var mission))
                continue;

            mission.Activity.Add(MapActivityRowToModel(row));
        }
    }

    private MissionCardModel MapMissionRowToModel(MissionCardJoinedRow row)
    {
        if (!Enum.TryParse<MissionSubType>(row.SubType, ignoreCase: true, out var subType))
            subType = MissionSubType.Stable;

        var model = new MissionCardModel
        {
            Id = row.MissionCardID,
            CardID = row.CardID,
            Title = row.Title ?? string.Empty,
            Tags = row.Tags ?? string.Empty,
            Status = row.Status ?? string.Empty,
            Description = row.Description ?? string.Empty,
            SubType = subType,
            Value = row.Value,
            ValuePerMinute = row.ValuePerMinute,
            CreatedDate = ParseInstantUtc(row.CreatedDate),
            AvailableFromDate = ParseLocalDateTime(row.AvailableFromDate),
            DueDate = ParseLocalDateTime(row.DueDate),
            EventDate = string.IsNullOrWhiteSpace(row.EventDate)
                ? null
                : ParseLocalDateTime(row.EventDate!),
            EstCompletionTime = StringToTimeSpan(row.EstCompletionTimeText),
            IsFailed = string.IsNullOrWhiteSpace(row.CompletedDate) && row.IsFailed != 0,
            Activity = new List<ActivityModel>()
        };

        if (string.IsNullOrWhiteSpace(row.CompletedDate))
            return model;

        var completedAt = ParseInstantUtc(row.CompletedDate!);
        if (row.IsFailed != 0)
            model.Fail(completedAt);
        else
            model.Complete(completedAt);

        return model;
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

    private static string AppendWhereClause(string sql, string? whereClause)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            return sql + ";";

        var clause = whereClause.Trim().TrimEnd(';');
        if (clause.StartsWith("WHERE", StringComparison.OrdinalIgnoreCase) ||
            clause.StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase) ||
            clause.StartsWith("LIMIT", StringComparison.OrdinalIgnoreCase))
        {
            return sql + " " + clause + ";";
        }

        return sql + " WHERE " + clause + ";";
    }

    private DateTime ParseInstantUtc(string value)
    {
        return LegacyTimeReader.ReadInstantUtc(value, _timeZoneService).UtcInstant;
    }

    private string SerializeInstantForDb(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? StrictTimeSerializer.SerializeUtcInstant(value)
            : StrictTimeSerializer.SerializeUtcInstantFromLocal(value, _timeZoneService);
    }

    private string? SerializeNullableInstantForDb(DateTime? value)
    {
        return value.HasValue ? SerializeInstantForDb(value.Value) : null;
    }

    private static DateTime ParseLocalDateTime(string value)
    {
        return LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime;
    }

    private static TimeSpan? StringToTimeSpan(string? estCompletionTimeText)
    {
        if (string.IsNullOrEmpty(estCompletionTimeText))
            return null;

        var parts = estCompletionTimeText.Split(':');
        var hours = int.Parse(parts[0]);
        var minutes = int.Parse(parts[1]);
        var seconds = int.Parse(parts[2]);

        return new TimeSpan(hours, minutes, seconds);
    }

    private sealed class MissionCardJoinedRow
    {
        public int MissionCardID { get; set; }
        public long CardID { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? SubType { get; set; }
        public double Value { get; set; }
        public string CreatedDate { get; set; } = "";
        public string AvailableFromDate { get; set; } = "";
        public string DueDate { get; set; } = "";
        public string? CompletedDate { get; set; }
        public string? EventDate { get; set; }
        public string? EstCompletionTimeText { get; set; }
        public int IsFailed { get; set; }
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
