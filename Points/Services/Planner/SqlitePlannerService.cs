using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;

namespace Points.Services.Planner;

public sealed class SqlitePlannerService : IPlannerService
{
    private readonly ISqliteConnectionContext _context;
    private readonly IPlannerCardSource _cards;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IClock _clock;

    public SqlitePlannerService(
        ISqliteConnectionContext context,
        IPlannerCardSource cards,
        ITimeZoneService timeZoneService,
        IClock clock)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<PlannerDayData> GetPlannerDayDataAsync(DateTime plannerDate)
    {
        await _context.InitializeAsync();

        var start = ToLocalWallClockForComparison(plannerDate.Date);
        var end = start.AddDays(1);
        var completedRangeUtc = ToInstantQueryUtcRange(start, end);

        var planner = await GetPlannerForDateAsync(start);
        var mainQuest = await _cards.GetMainQuestModelsDataAsync(start, end);

        var missions = (await _cards.GetMissionCardModelsDataAsync())
            .Where(m =>
                !m.CompletedDate.HasValue
                || IsInstantInHalfOpenRange(ToUtcInstantForWrite(m.CompletedDate.Value), completedRangeUtc)
                || LocalDateTimeRangesOverlap(m.AvailableFromDate, m.DueDate, start, end))
            .ToList();

        return new PlannerDayData
        {
            Planner = planner,
            TaskCards = mainQuest.Concat(missions.Cast<IActiveCardModel>()).ToList(),
            ScCards = mainQuest.OfType<ScCardModel>().ToList(),
            MissionCards = missions
        };
    }

    public async Task SavePlannerAsync(PlannerModel planner)
    {
        await _context.InitializeAsync();

        if (planner == null)
            throw new ArgumentNullException(nameof(planner));

        NormalizePlannerForSave(planner);
        ValidatePlannerTasks(planner.Tasks);

        var plannerDate = planner.PlannerDate.Date;
        var dateKey = ToPlannerDateKey(plannerDate);
        var nowIso = StrictTimeSerializer.SerializeUtcInstant(_clock.UtcNow);
        var plannerId = planner.PlannerId;

        await _context.RunInTransactionAsync(tran =>
        {
            var existing = tran.Query<PlannerIdRow>(
                "SELECT PlannerID FROM Planner WHERE PlannerDate = ? LIMIT 1;",
                dateKey).FirstOrDefault();

            if (existing == null)
            {
                tran.Execute(
                    "INSERT INTO Planner (PlannerDate, CreatedAt, UpdatedAt) VALUES (?, ?, ?);",
                    dateKey,
                    nowIso,
                    nowIso);

                plannerId = tran.ExecuteScalar<long>("SELECT last_insert_rowid();");
            }
            else
            {
                plannerId = existing.PlannerID;
                tran.Execute(
                    "UPDATE Planner SET UpdatedAt = ? WHERE PlannerID = ?;",
                    nowIso,
                    plannerId);
            }

            tran.Execute("DELETE FROM PlannerTask WHERE PlannerID = ?;", plannerId);
            tran.Execute("DELETE FROM PlannerEvent WHERE PlannerID = ?;", plannerId);

            foreach (var task in planner.Tasks.OrderBy(t => t.PlannedStart))
            {
                tran.Execute(@"
                    INSERT INTO PlannerTask
                        (PlannerID, CardID, CardKind, PlannedStart, PlannedEnd)
                    VALUES (?, ?, ?, ?, ?);
                ",
                plannerId,
                task.CardId,
                task.CardKind.ToString(),
                SerializePlannerLocalDateTime(task.PlannedStart),
                SerializePlannerLocalDateTime(task.PlannedEnd));
            }

            foreach (var ev in planner.Events.OrderBy(e => e.PlannedTime))
            {
                tran.Execute(@"
                    INSERT INTO PlannerEvent
                        (PlannerID, EventKind, CardID, ScCardStepID, PlannedTime, PlannedCount)
                    VALUES (?, ?, ?, ?, ?, ?);
                ",
                plannerId,
                ev.EventKind.ToString(),
                ev.CardId,
                ev.ScCardStepId,
                SerializePlannerLocalDateTime(ev.PlannedTime),
                Math.Max(1, ev.PlannedCount));
            }
        });

        planner.PlannerId = plannerId;
        foreach (var task in planner.Tasks)
            task.PlannerId = plannerId;
        foreach (var ev in planner.Events)
            ev.PlannerId = plannerId;
    }

    private async Task<PlannerModel?> GetPlannerForDateAsync(DateTime plannerDate)
    {
        var dateKey = ToPlannerDateKey(plannerDate.Date);

        var row = (await _context.Db.QueryAsync<PlannerRow>(
            "SELECT PlannerID, PlannerDate FROM Planner WHERE PlannerDate = ? LIMIT 1;",
            dateKey)).FirstOrDefault();

        if (row == null)
            return null;

        var planner = new PlannerModel
        {
            PlannerId = row.PlannerID,
            PlannerDate = StrictTimeSerializer.ParseLocalDate(row.PlannerDate)
        };

        var tasks = await _context.Db.QueryAsync<PlannerTaskRow>(@"
            SELECT PlannerTaskID, PlannerID, CardID, CardKind, PlannedStart, PlannedEnd
            FROM PlannerTask
            WHERE PlannerID = ?
            ORDER BY PlannedStart;
        ", row.PlannerID);

        foreach (var task in tasks)
        {
            if (!Enum.TryParse<PlannerTaskCardKind>(task.CardKind, true, out var kind))
                kind = PlannerTaskCardKind.TatCard;

            planner.Tasks.Add(new PlannerTaskModel
            {
                PlannerTaskId = task.PlannerTaskID,
                PlannerId = task.PlannerID,
                CardId = task.CardID,
                CardKind = kind,
                PlannedStart = ReadPlannerLocalDateTime(task.PlannedStart),
                PlannedEnd = ReadPlannerLocalDateTime(task.PlannedEnd)
            });
        }

        var events = await _context.Db.QueryAsync<PlannerEventRow>(@"
            SELECT PlannerEventID, PlannerID, EventKind, CardID, ScCardStepID, PlannedTime, PlannedCount
            FROM PlannerEvent
            WHERE PlannerID = ?
            ORDER BY PlannedTime;
        ", row.PlannerID);

        foreach (var ev in events)
        {
            if (!Enum.TryParse<PlannerEventKind>(ev.EventKind, true, out var kind))
                kind = PlannerEventKind.ScStepRep;

            planner.Events.Add(new PlannerEventModel
            {
                PlannerEventId = ev.PlannerEventID,
                PlannerId = ev.PlannerID,
                EventKind = kind,
                CardId = ev.CardID,
                ScCardStepId = ev.ScCardStepID,
                PlannedTime = ReadPlannerLocalDateTime(ev.PlannedTime),
                PlannedCount = Math.Max(1, ev.PlannedCount)
            });
        }

        return planner;
    }

    private void NormalizePlannerForSave(PlannerModel planner)
    {
        planner.PlannerDate = ToLocalWallClockForComparison(planner.PlannerDate).Date;

        foreach (var task in planner.Tasks)
        {
            task.PlannedStart = ToLocalWallClockForComparison(task.PlannedStart);
            task.PlannedEnd = ToLocalWallClockForComparison(task.PlannedEnd);
        }

        foreach (var ev in planner.Events)
        {
            ev.PlannedTime = ToLocalWallClockForComparison(ev.PlannedTime);
        }
    }

    private string ToPlannerDateKey(DateTime plannerDate)
    {
        return StrictTimeSerializer.SerializeLocalDate(ToLocalWallClockForComparison(plannerDate).Date);
    }

    private string SerializePlannerLocalDateTime(DateTime value)
    {
        return StrictTimeSerializer.SerializeLocalDateTime(ToLocalWallClockForComparison(value));
    }

    private static DateTime ReadPlannerLocalDateTime(string value)
    {
        return LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime;
    }

    private DateTime ToUtcInstantForWrite(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return WithKind(value, DateTimeKind.Utc);

        return value.Kind == DateTimeKind.Utc
            ? StrictTimeSerializer.RequireUtcInstant(value, nameof(value))
            : _timeZoneService.ToUtcFromLocal(value);
    }

    private UtcDateTimeRange ToInstantQueryUtcRange(DateTime rangeStart, DateTime rangeEnd)
    {
        return new UtcDateTimeRange(
            ToUtcInstantForWrite(rangeStart),
            ToUtcInstantForWrite(rangeEnd));
    }

    private DateTime ToLocalWallClockForComparison(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return WithKind(value, DateTimeKind.Unspecified);

        var local = value.Kind == DateTimeKind.Utc
            ? _timeZoneService.ToLocal(value)
            : value;

        return WithKind(local, DateTimeKind.Unspecified);
    }

    private static DateTime WithKind(DateTime value, DateTimeKind kind)
    {
        return new DateTime(value.Ticks, kind);
    }

    private bool LocalDateTimeRangesOverlap(DateTime leftStart, DateTime leftEnd, DateTime rightStart, DateTime rightEnd)
    {
        leftStart = ToLocalWallClockForComparison(leftStart);
        leftEnd = ToLocalWallClockForComparison(leftEnd);
        rightStart = ToLocalWallClockForComparison(rightStart);
        rightEnd = ToLocalWallClockForComparison(rightEnd);

        return leftStart < rightEnd && leftEnd >= rightStart;
    }

    private static bool IsInstantInHalfOpenRange(DateTime utcInstant, UtcDateTimeRange range)
    {
        utcInstant = StrictTimeSerializer.RequireUtcInstant(utcInstant, nameof(utcInstant));
        return utcInstant >= range.StartUtc && utcInstant < range.EndUtc;
    }

    private static void ValidatePlannerTasks(IEnumerable<PlannerTaskModel> tasks)
    {
        var ordered = tasks
            .OrderBy(t => t.PlannedStart)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].PlannedEnd <= ordered[i].PlannedStart)
                throw new InvalidOperationException("Planner task end time must be after start time.");

            if (i > 0 && ordered[i].PlannedStart < ordered[i - 1].PlannedEnd)
                throw new InvalidOperationException("Planner task blocks cannot overlap.");
        }
    }

    private sealed class PlannerIdRow
    {
        public long PlannerID { get; set; }
    }

    private sealed class PlannerRow
    {
        public long PlannerID { get; set; }
        public string PlannerDate { get; set; } = "";
    }

    private sealed class PlannerTaskRow
    {
        public long PlannerTaskID { get; set; }
        public long PlannerID { get; set; }
        public long CardID { get; set; }
        public string CardKind { get; set; } = "";
        public string PlannedStart { get; set; } = "";
        public string PlannedEnd { get; set; } = "";
    }

    private sealed class PlannerEventRow
    {
        public long PlannerEventID { get; set; }
        public long PlannerID { get; set; }
        public string EventKind { get; set; } = "";
        public long CardID { get; set; }
        public int? ScCardStepID { get; set; }
        public string PlannedTime { get; set; } = "";
        public int PlannedCount { get; set; }
    }
}
