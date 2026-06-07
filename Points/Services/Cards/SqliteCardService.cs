using Points.Services.Sqlite;
using Points.Models;
using Points.Services;
using Points.Services.Planner;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Services.Trackers;
using SQLite;

namespace Points.Services.Cards;

public sealed class SqliteCardService : ICardReadService, ICardWriteService, IPlannerCardSource
{
    private const string ArchivedStatus = "Archived";

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

        var budget = (await _budgets.GetBudgetCardModelsDataAsync())
            .Where(IsNotArchived)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CardID)
            .ToList();
        var achievements = SortByDisplayOrder(await _achievements.GetAchievementCardModelsDataAsync());

        await _achievements.PopulateAchievementsAsync(achievements, mainQuest, mission);
        await PopulateLocksAsync(mainQuest, mission);

        var valueTrackers = (await _trackers.GetValueTrackerCardModelsDataAsync())
            .Where(IsNotArchived)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CardID)
            .ToList();
        var eventTrackers = (await _trackers.GetEventTrackerCardModelsDataAsync())
            .Where(IsNotArchived)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CardID)
            .ToList();

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
        var tats = (await _tatCards.GetTatModelsDataAsync(rangeStart, rangeEnd))
            .Where(IsNotArchived)
            .ToList();
        var scs = (await _scCards.GetScModelsDataAsync(rangeStart, rangeEnd))
            .Where(IsNotArchived)
            .ToList();

        var mainQuest = new List<IActiveCardModel>();
        mainQuest.AddRange(tats);
        mainQuest.AddRange(scs);

        return SortByDisplayOrder(mainQuest).ToList();
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
                    "INSERT INTO Card (DisplayOrder, Title, Tags) VALUES (?, ?, ?);",
                    model.DisplayOrder,
                    model.Title,
                    model.Tags);

                cardId = await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            }
            else
            {
                await _context.Db.ExecuteAsync(
                    "UPDATE Card SET DisplayOrder = ?, Title = ?, Tags = ? WHERE CardID = ?;",
                    model.DisplayOrder,
                    model.Title,
                    model.Tags,
                    cardId.Value);
            }

            model.CardID = cardId.Value;

            await SaveSubtypeAsync(model, cardId.Value);
        }
    }

    public async Task SaveCardDisplayOrderAsync(IReadOnlyList<ICardModel> orderedCards)
    {
        if (orderedCards == null)
            throw new ArgumentNullException(nameof(orderedCards));

        await _context.InitializeAsync();

        await _context.RunInTransactionAsync(conn =>
        {
            for (var i = 0; i < orderedCards.Count; i++)
            {
                var card = orderedCards[i];
                if (card == null || card.CardID <= 0)
                    continue;

                card.DisplayOrder = i;
                conn.Execute(
                    "UPDATE Card SET DisplayOrder = ? WHERE CardID = ?;",
                    card.DisplayOrder,
                    card.CardID);
            }
        });
    }

    public async Task DeleteCardModelAsync(ICardModel model)
    {
        await _context.InitializeAsync();

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var cardId = await ResolveCardIdForDeleteAsync(model);
        if (cardId == null)
            return;

        var archived = false;

        await _context.Db.RunInTransactionAsync(conn =>
        {
            if (HasTransactionalData(conn, model, cardId.Value))
            {
                ArchiveSubtype(conn, model, cardId.Value);
                archived = true;
                return;
            }

            DeleteCommonCardRows(conn, cardId.Value);
            conn.Execute("DELETE FROM Card WHERE CardID = ?;", cardId.Value);
        });

        if (archived)
        {
            SetModelArchived(model);
            return;
        }

        UdmdImageFileStore.TryDeleteCardFolder(cardId.Value);
        model.Id = 0;
        model.CardID = 0;
    }

    public async Task<bool> WouldArchiveCardModelOnDeleteAsync(ICardModel model)
    {
        await _context.InitializeAsync();

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var cardId = await ResolveCardIdForDeleteAsync(model);
        if (cardId == null)
            return false;

        return model switch
        {
            ScCardModel => await HasRowsAsync("SELECT 1 FROM Activity WHERE CardID = ? LIMIT 1;", cardId.Value) ||
                           await HasRowsAsync(
                               @"SELECT 1
                                 FROM ScCard sc
                                 JOIN ScCardStep step ON step.ScCardID = sc.ScCardID
                                 JOIN ScCardStepRep rep ON rep.ScCardStepID = step.ScCardStepID
                                 WHERE sc.CardID = ?
                                 LIMIT 1;",
                               cardId.Value),
            TatCardModel => await HasRowsAsync("SELECT 1 FROM Activity WHERE CardID = ? LIMIT 1;", cardId.Value),
            BudgetCardModel => await HasBudgetTransactionRowsAsync(model.Id, cardId.Value),
            TrackerCardModel => await HasRowsAsync("SELECT 1 FROM TrackerValue WHERE CardID = ? LIMIT 1;", cardId.Value),
            _ => false
        };
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

    private async Task<long?> ResolveCardIdForDeleteAsync(ICardModel model)
    {
        var cardId = model.CardID;
        if (cardId > 0)
            return cardId;

        return await CheckForCardIdAsync(model);
    }

    private async Task<bool> HasBudgetTransactionRowsAsync(int modelId, long cardId)
    {
        var budgetCardId = modelId > 0
            ? modelId
            : (int)(await _context.Db.QueryScalarsAsync<long>(
                "SELECT BudgetCardID FROM BudgetCard WHERE CardID = ? LIMIT 1;",
                cardId)).FirstOrDefault();

        return budgetCardId > 0 &&
               await HasRowsAsync("SELECT 1 FROM BudgetCardTransaction WHERE BudgetCardID = ? LIMIT 1;", budgetCardId);
    }

    private async Task<bool> HasRowsAsync(string sql, params object[] args)
    {
        return (await _context.Db.QueryScalarsAsync<int>(sql, args)).FirstOrDefault() != 0;
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
        if (ids.Count == 0)
            return;

        const int chunkSize = 500;

        for (var i = 0; i < ids.Count; i += chunkSize)
        {
            var chunk = ids.Skip(i).Take(chunkSize).ToArray();
            var placeholders = string.Join(",", Enumerable.Repeat("?", chunk.Length));
            var sql = $"DELETE FROM {table} WHERE {idColumn} IN ({placeholders});";
            conn.Execute(sql, chunk.Cast<object>().ToArray());
        }
    }

    private static void DeleteCommonCardRows(SQLiteConnection conn, long cardId)
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
    }

    private static bool HasTransactionalData(SQLiteConnection conn, ICardModel model, long cardId)
    {
        return model switch
        {
            ScCardModel => HasActivityRows(conn, cardId) || HasScRepRows(conn, cardId),
            TatCardModel => HasActivityRows(conn, cardId),
            BudgetCardModel => HasBudgetTransactionRows(conn, model.Id, cardId),
            TrackerCardModel => HasTrackerValueRows(conn, cardId),
            _ => false
        };
    }

    private static bool HasActivityRows(SQLiteConnection conn, long cardId)
    {
        return HasRows(conn, "SELECT 1 FROM Activity WHERE CardID = ? LIMIT 1;", cardId);
    }

    private static bool HasScRepRows(SQLiteConnection conn, long cardId)
    {
        return HasRows(
            conn,
            @"SELECT 1
              FROM ScCard sc
              JOIN ScCardStep step ON step.ScCardID = sc.ScCardID
              JOIN ScCardStepRep rep ON rep.ScCardStepID = step.ScCardStepID
              WHERE sc.CardID = ?
              LIMIT 1;",
            cardId);
    }

    private static bool HasBudgetTransactionRows(SQLiteConnection conn, int modelId, long cardId)
    {
        var budgetCardId = modelId > 0
            ? modelId
            : (int)conn.QueryScalars<long>(
                "SELECT BudgetCardID FROM BudgetCard WHERE CardID = ? LIMIT 1;",
                cardId).FirstOrDefault();

        return budgetCardId > 0 &&
               HasRows(conn, "SELECT 1 FROM BudgetCardTransaction WHERE BudgetCardID = ? LIMIT 1;", budgetCardId);
    }

    private static bool HasTrackerValueRows(SQLiteConnection conn, long cardId)
    {
        return HasRows(conn, "SELECT 1 FROM TrackerValue WHERE CardID = ? LIMIT 1;", cardId);
    }

    private static bool HasRows(SQLiteConnection conn, string sql, params object[] args)
    {
        return conn.QueryScalars<int>(sql, args).FirstOrDefault() != 0;
    }

    private static void ArchiveSubtype(SQLiteConnection conn, ICardModel model, long cardId)
    {
        switch (model)
        {
            case ScCardModel:
                conn.Execute("UPDATE ScCard SET Status = ? WHERE CardID = ?;", ArchivedStatus, cardId);
                break;
            case TatCardModel:
                conn.Execute("UPDATE TatCard SET Status = ? WHERE CardID = ?;", ArchivedStatus, cardId);
                break;
            case BudgetCardModel:
                conn.Execute("UPDATE BudgetCard SET Status = ? WHERE CardID = ?;", ArchivedStatus, cardId);
                break;
            case ValueTrackerCardModel:
                conn.Execute("UPDATE ValueTrackerCard SET Status = ? WHERE CardID = ?;", ArchivedStatus, cardId);
                break;
            case EventTrackerCardModel:
                conn.Execute("UPDATE EventTrackerCard SET Status = ? WHERE CardID = ?;", ArchivedStatus, cardId);
                break;
        }
    }

    private static void SetModelArchived(ICardModel model)
    {
        switch (model)
        {
            case BudgetCardModel budget:
                budget.Status = ArchivedStatus;
                break;
            case TrackerCardModel tracker:
                tracker.Status = ArchivedStatus;
                break;
            case TatCardModel tat:
                tat.Status = ArchivedStatus;
                break;
        }
    }

    private static bool IsNotArchived(TatCardModel card)
    {
        return !IsArchivedStatus(card.Status);
    }

    private static bool IsNotArchived(BudgetCardModel card)
    {
        return !IsArchivedStatus(card.Status);
    }

    private static bool IsNotArchived(TrackerCardModel card)
    {
        return !IsArchivedStatus(card.Status);
    }

    private static bool IsArchivedStatus(string? status)
    {
        return string.Equals(status?.Trim(), ArchivedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static List<TCard> SortByDisplayOrder<TCard>(IEnumerable<TCard> cards)
        where TCard : ICardModel
    {
        return cards
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CardID)
            .ThenBy(c => c.Id)
            .ToList();
    }

    private sealed class CardTitleRow
    {
        public string Title { get; set; } = "";
    }
}
