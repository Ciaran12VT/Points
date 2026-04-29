using Points.Models;
using Points.Services;
using Points.Services.Planner;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.Services.Trackers;
using SQLite;

namespace Points.Services.Cards;

public sealed class SqliteCardService : ICardReadService, ICardWriteService, IPlannerCardSource
{
    private readonly ISqliteConnectionContext _context;
    private readonly ITimeZoneService _timeZoneService;
    private readonly ITatCardService _tatCards;
    private readonly IScCardService _scCards;
    private readonly IMissionCardService _missionCards;
    private readonly IBudgetService _budgets;
    private readonly IAchievementService _achievements;
    private readonly ITrackerService _trackers;
    private readonly ILockService _locks;

    public SqliteCardService(
        ISqliteConnectionContext context,
        ITimeZoneService timeZoneService,
        ITatCardService tatCards,
        IScCardService scCards,
        IMissionCardService missionCards,
        IBudgetService budgets,
        IAchievementService achievements,
        ITrackerService trackers,
        ILockService locks)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _tatCards = tatCards ?? throw new ArgumentNullException(nameof(tatCards));
        _scCards = scCards ?? throw new ArgumentNullException(nameof(scCards));
        _missionCards = missionCards ?? throw new ArgumentNullException(nameof(missionCards));
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        _achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
        _trackers = trackers ?? throw new ArgumentNullException(nameof(trackers));
        _locks = locks ?? throw new ArgumentNullException(nameof(locks));
    }

    public Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync()
    {
        return _achievements.GetAchievementCardModelsDataAsync();
    }

    public Task<List<TrophyModel>> GetTrophyModelsDataAsync()
    {
        return _achievements.GetTrophyModelsDataAsync();
    }

    public async Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd)
    {
        var mainQuest = await GetMainQuestModelsDataAsync(rangeStart, rangeEnd);

        var missionRangeUtc = ToInstantQueryUtcRange(rangeStart, rangeEnd);
        var mission = (await _missionCards.GetMissionCardModelsDataAsync())
            .Where(m => !m.CompletedDate.HasValue ||
                        InstantFallsInUtcRange(ToUtcInstantForWrite(m.CompletedDate.Value), missionRangeUtc))
            .ToList();

        var budget = await _budgets.GetBudgetCardModelsDataAsync();
        var achievements = await _achievements.GetAchievementCardModelsDataAsync();

        await _achievements.PopulateAchievementsAsync(achievements, mainQuest, mission);
        await PopulateLocksAsync(mainQuest, mission);

        var valueTrackers = await _trackers.GetValueTrackerCardModelsDataAsync();
        var eventTrackers = await _trackers.GetEventTrackerCardModelsDataAsync();

        return new HomeSeedData
        {
            MainQuestCards = mainQuest,
            MissionCards = mission,
            BudgetCards = budget,
            Achievements = achievements,
            ValueTrackers = valueTrackers,
            EventTrackers = eventTrackers
        };
    }

    public async Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
    {
        var tats = await _tatCards.GetTatModelsDataAsync(rangeStart, rangeEnd);
        var scs = await _scCards.GetScModelsDataAsync(rangeStart, rangeEnd);

        var mainQuest = new List<IActiveCardModel>();
        mainQuest.AddRange(tats);
        mainQuest.AddRange(scs);

        return mainQuest;
    }

    public Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null)
    {
        return _missionCards.GetMissionCardModelsDataAsync(whereClause);
    }

    public async Task<string?> GetCardTitleByIdAsync(long cardId)
    {
        await _context.InitializeAsync();

        var rows = await _context.Db.QueryAsync<CardTitleRow>(
            @"SELECT Title
              FROM Card
              WHERE CardId = ?;",
            cardId);

        return rows.FirstOrDefault()?.Title ?? "";
    }

    public async Task SaveCardModelAsync(ICardModel model)
    {
        await SaveCardModelsAsync(new List<ICardModel> { model });
    }

    public async Task SaveCardModelsAsync(List<ICardModel> models)
    {
        if (models == null)
            throw new ArgumentNullException(nameof(models));

        await _context.InitializeAsync();

        foreach (var model in models)
        {
            if (model == null)
                throw new ArgumentException("Card list cannot contain null models.", nameof(models));

            var cardId = await CheckForCardIdAsync(model);

            if (cardId == null)
            {
                await _context.Db.ExecuteAsync(
                    "INSERT INTO Card (Title, Tags) VALUES (?, ?);",
                    model.Title,
                    model.Tags);

                cardId = await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await _context.Db.ExecuteAsync(
                    "UPDATE Card SET Title = ?, Tags = ? WHERE CardID = ?;",
                    model.Title,
                    model.Tags,
                    cardId.Value);
            }

            model.CardID = cardId.Value;

            await SaveSubtypeAsync(model, cardId.Value);
        }
    }

    public async Task DeleteCardModelAsync(ICardModel model)
    {
        await _context.InitializeAsync();

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var cardId = model.CardID;
        if (cardId <= 0)
        {
            var resolvedCardId = await CheckForCardIdAsync(model);
            if (resolvedCardId == null)
                return;

            cardId = resolvedCardId.Value;
        }

        await _context.Db.RunInTransactionAsync(conn =>
        {
            conn.Execute("DELETE FROM Shortcut WHERE TargetCardId = ?;", cardId);
            conn.Execute("DELETE FROM NotificationLog WHERE CardId = ?;", cardId);
            conn.Execute("DELETE FROM CardSchedule WHERE CardId = ?;", cardId);
            conn.Execute("DELETE FROM LockTaskDependency WHERE TaskDependencyCardId = ?;", cardId);

            var lockIds = conn.QueryScalars<long>("SELECT LockId FROM Lock WHERE CardId = ?;", cardId);
            var lockIdList = lockIds.ToList();
            DeleteByIds(conn, "LockSchedule", "LockId", lockIdList);
            DeleteByIds(conn, "LockTaskDependency", "LockId", lockIdList);
            conn.Execute("DELETE FROM Lock WHERE CardId = ?;", cardId);

            conn.Execute("DELETE FROM Card WHERE CardID = ?;", cardId);
        });

        UdmdImageFileStore.TryDeleteCardFolder(cardId);

        model.Id = 0;
        model.CardID = 0;
    }

    private async Task SaveSubtypeAsync(ICardModel model, long cardId)
    {
        switch (model)
        {
            case ScCardModel sc:
                await _scCards.SaveScModelDataAsync(sc, cardId);
                break;
            case TatCardModel tat:
                await _tatCards.SaveTatModelDataAsync(tat, cardId);
                break;
            case MissionCardModel mission:
                await _missionCards.SaveMissionCardModelDataAsync(mission, cardId);
                break;
            case BudgetCardModel budget:
                await _budgets.SaveBudgetCardModelDataAsync(budget, cardId);
                break;
            case AchievementCardModel achievement:
                await _achievements.SaveAchievementCardModelDataAsync(achievement, cardId);
                break;
            case ValueTrackerCardModel valueTracker:
                await _trackers.SaveValueTrackerCardModelDataAsync(valueTracker, cardId);
                break;
            case EventTrackerCardModel eventTracker:
                await _trackers.SaveEventTrackerCardModelDataAsync(eventTracker, cardId);
                break;
        }
    }

    private async Task<long?> CheckForCardIdAsync(ICardModel model)
    {
        var cardId = model switch
        {
            ScCardModel => await QuerySubtypeCardIdAsync("ScCard", "ScCardID", model.Id),
            TatCardModel => await QuerySubtypeCardIdAsync("TatCard", "TatCardID", model.Id),
            MissionCardModel => await QuerySubtypeCardIdAsync("MissionCard", "MissionCardID", model.Id),
            BudgetCardModel => await QuerySubtypeCardIdAsync("BudgetCard", "BudgetCardID", model.Id),
            AchievementCardModel => await QuerySubtypeCardIdAsync("AchievementCard", "AchievementCardID", model.Id),
            ValueTrackerCardModel => await QuerySubtypeCardIdAsync("ValueTrackerCard", "ValueTrackerCardID", model.Id),
            EventTrackerCardModel => await QuerySubtypeCardIdAsync("EventTrackerCard", "EventTrackerCardID", model.Id),
            _ => null
        };

        return cardId > 0 ? cardId : null;
    }

    private async Task<long?> QuerySubtypeCardIdAsync(string tableName, string idColumn, int subtypeId)
    {
        var ids = await _context.Db.QueryScalarsAsync<long>(
            $"SELECT CardID FROM {tableName} WHERE {idColumn} = ? LIMIT 1;",
            subtypeId);

        var cardId = ids.FirstOrDefault();
        return cardId <= 0 ? null : cardId;
    }

    private async Task PopulateLocksAsync(List<IActiveCardModel> mainQuest, List<MissionCardModel> mission)
    {
        var activeCards = mainQuest
            .Concat(mission.Cast<IActiveCardModel>())
            .ToList();

        var cardIds = activeCards
            .Select(c => c.CardID)
            .Distinct()
            .ToList();

        var locksByCardId = new Dictionary<long, List<LockModel>>();
        foreach (var id in cardIds)
            locksByCardId[id] = await _locks.GetLocksForCardAsync(id);

        foreach (var card in activeCards)
        {
            card.Locks = locksByCardId.TryGetValue(card.CardID, out var cardLocks)
                ? cardLocks
                : new List<LockModel>();
        }
    }

    private DateTime ToUtcInstantForWrite(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return new DateTime(value.Ticks, DateTimeKind.Utc);

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

    private static bool InstantFallsInUtcRange(DateTime utcInstant, UtcDateTimeRange range)
    {
        utcInstant = StrictTimeSerializer.RequireUtcInstant(utcInstant, nameof(utcInstant));
        return utcInstant >= range.StartUtc && utcInstant <= range.EndUtc;
    }

    private static void DeleteByIds(SQLiteConnection conn, string table, string idColumn, List<long> ids)
    {
        const int chunkSize = 500;

        for (var i = 0; i < ids.Count; i += chunkSize)
        {
            var chunk = ids.Skip(i).Take(chunkSize).ToArray();
            var placeholders = string.Join(",", Enumerable.Repeat("?", chunk.Length));
            var sql = $"DELETE FROM {table} WHERE {idColumn} IN ({placeholders});";
            conn.Execute(sql, chunk.Cast<object>().ToArray());
        }
    }

    private sealed class CardTitleRow
    {
        public string Title { get; set; } = "";
    }
}
