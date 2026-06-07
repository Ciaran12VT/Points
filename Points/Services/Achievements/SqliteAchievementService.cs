using Points.Services.Sqlite;
using Points.Evaluators;
using Points.Global;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;
using SQLite;
using System.Security.Cryptography;

namespace Points.Services.Achievements;

public sealed class SqliteAchievementService : IAchievementService
{
    private readonly ISqliteConnectionContext _context;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IClock _clock;
    private readonly Func<int, string> _achievementTrophiesPathFactory;

    public SqliteAchievementService(
        ISqliteConnectionContext context,
        ITimeZoneService timeZoneService,
        IClock clock,
        Func<int, string>? achievementTrophiesPathFactory = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _achievementTrophiesPathFactory = achievementTrophiesPathFactory ?? AppPaths.GetAchievementTrophiesPath;
    }

    public async Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                a.AchievementCardID         AS AchievementCardID,
                a.CardID                    AS CardID,
                c.DisplayOrder              AS DisplayOrder,
                c.Title                     AS Title,
                c.Tags                      AS Tags,
                a.Status                    AS Status,
                a.Description               AS Description,
                a.TargetType                AS TargetType,
                a.DifficultyLevel           AS DifficultyLevel,
                a.CreatedDate               AS CreatedDate,
                a.LastEarnedAt              AS LastEarnedAt,
                a.TargetActiveTimeInSeconds AS TargetActiveTimeInSeconds,
                a.TargetValue               AS TargetValue,
                a.ScCardStepID              AS ScCardStepID,
                a.CompletionType            AS CompletionType,
                a.RangeUnit                 AS RangeUnit,
                a.RangeAmount               AS RangeAmount,
                a.DeadlineStart             AS DeadlineStart,
                a.Deadline                  AS Deadline,
                a.FinalizedAt               AS FinalizedAt,
                a.FrozenCurrentValue        AS FrozenCurrentValue,
                a.TrophyURLs                AS TrophyURLs,
                a.IsPinned                  AS IsPinned
            FROM AchievementCard a
            JOIN Card c ON c.CardID = a.CardID
            WHERE a.AchievementCardID = ?
            LIMIT 1;";

        var row = (await _context.Db.QueryAsync<AchievementCardJoinedRow>(sql, id)).FirstOrDefault();
        if (row == null)
            throw new KeyNotFoundException($"AchievementCard not found. AchievementCardID={id}");

        var model = MapAchievementRowToModel(row);
        return await FinalizeDeadlineAchievementIfNeededAsync(model);
    }

    public async Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync()
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                a.AchievementCardID         AS AchievementCardID,
                a.CardID                    AS CardID,
                c.DisplayOrder              AS DisplayOrder,
                c.Title                     AS Title,
                c.Tags                      AS Tags,
                a.Status                    AS Status,
                a.Description               AS Description,
                a.TargetType                AS TargetType,
                a.DifficultyLevel           AS DifficultyLevel,
                a.CreatedDate               AS CreatedDate,
                a.LastEarnedAt              AS LastEarnedAt,
                a.TargetActiveTimeInSeconds AS TargetActiveTimeInSeconds,
                a.TargetValue               AS TargetValue,
                a.ScCardStepID              AS ScCardStepID,
                a.CompletionType            AS CompletionType,
                a.RangeUnit                 AS RangeUnit,
                a.RangeAmount               AS RangeAmount,
                a.DeadlineStart             AS DeadlineStart,
                a.Deadline                  AS Deadline,
                a.FinalizedAt               AS FinalizedAt,
                a.FrozenCurrentValue        AS FrozenCurrentValue,
                a.TrophyURLs                AS TrophyURLs,
                a.IsPinned                  AS IsPinned
            FROM AchievementCard a
            JOIN Card c ON c.CardID = a.CardID
            ORDER BY c.DisplayOrder, a.AchievementCardID;";

        var rows = await _context.Db.QueryAsync<AchievementCardJoinedRow>(sql);
        if (rows.Count == 0)
            return new List<AchievementCardModel>();

        var models = rows.Select(MapAchievementRowToModel).ToList();
        models = await FinalizeDeadlineAchievementsIfNeededAsync(models);

        var now = _clock.LocalNow;

        return models
            .Where(x => ShouldKeepLoadedAfterFinalization(x, now))
            .ToList();
    }

    public async Task<List<TrophyModel>> GetTrophyModelsDataAsync()
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                TrophyID AS Id,
                AchievementCardID AS AchievementId,
                Title,
                EarnedOn,
                ImageSource
            FROM AchievementTrophy
            ORDER BY EarnedOn DESC;";

        var rows = await _context.Db.QueryAsync<TrophyRow>(sql);

        return rows.Select(r => new TrophyModel
            {
                Id = r.Id,
                AchievementId = r.AchievementId,
                Title = r.Title ?? string.Empty,
                EarnedOn = ParseInstantUtc(r.EarnedOn),
                ImageSource = string.IsNullOrWhiteSpace(r.ImageSource)
                    ? "trophy.png"
                    : r.ImageSource
            })
            .OrderByDescending(t => t.EarnedOn)
            .ToList();
    }

    public async Task PopulateAchievementsAsync(
        List<AchievementCardModel> achievements,
        List<IActiveCardModel> mainQuest,
        List<MissionCardModel> mission)
    {
        var byTag = await BuildEvaluatorsByTag(achievements);

        foreach (var mq in mainQuest)
        {
            var tags = mq.Tags.Split(',').Select(x => x.Trim());
            var evals = byTag.Where(x => tags.Contains(x.Key)).Select(y => y.Value).ToList();
            mq.TimeValueAchievementEvaluators = evals;
        }

        foreach (var ach in achievements)
        {
            if (byTag.TryGetValue(ach.Tags, out var evaluator))
            {
                var relevantEvaluations = evaluator.Evaluations?
                    .Where(x => x?.AchievementCard != null && x.AchievementCard.Id == ach.Id)
                    .ToList()
                    ?? new List<TimeValueAchievementEvaluation>();

                if (ach.TargetType == AchievementTargetType.Value)
                    ach.CurrentValue = relevantEvaluations.Sum(x => x.CurrentValue);
                else if (ach.TargetType == AchievementTargetType.ActiveTime)
                    ach.CurrentValue = relevantEvaluations.Sum(x => x.CurrentValue);
            }
            else
            {
                ach.CurrentValue = 0;
            }

            ach.NotifyTimeChanged();
        }
    }

    public async Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(List<TimeValueAchievementEvaluator> evaluators)
    {
        await _context.InitializeAsync();

        if (evaluators == null)
            return new List<TimeValueAchievementEvaluator>();

        var input = evaluators.ToList();
        if (input.Count == 0)
            return new List<TimeValueAchievementEvaluator>();

        var refreshed = new List<TimeValueAchievementEvaluator>(input.Count);

        foreach (var evaluator in input)
        {
            var newEvaluator = new TimeValueAchievementEvaluator
            {
                Evaluations = new List<TimeValueAchievementEvaluation>()
            };

            if (evaluator.Evaluations == null || evaluator.Evaluations.Count == 0)
            {
                refreshed.Add(newEvaluator);
                continue;
            }

            var achievementIds = evaluator.Evaluations
                .Where(e => e?.AchievementCard != null)
                .Select(e => e!.AchievementCard.Id)
                .Distinct()
                .ToList();

            var tasks = achievementIds.Select(async id =>
            {
                var ach = await GetAchievementCardModelDataAsync(id);
                return await CreateEvaluation(ach);
            });

            var evaluations = await Task.WhenAll(tasks);

            newEvaluator.Evaluations = evaluations.ToList();
            refreshed.Add(newEvaluator);
        }

        return refreshed;
    }

    public async Task SaveAchievementCardModelDataAsync(AchievementCardModel acm, long cardId)
    {
        await _context.InitializeAsync();

        if (acm == null)
            throw new ArgumentNullException(nameof(acm));

        ValidateAchievementForPersistence(acm);

        var now = _clock.UtcNow;
        var targetTypeText = acm.TargetType.ToString();
        var difficultyText = acm.Difficulty.ToString();
        var completionTypeText = acm.CompletionType.ToString();

        int? targetActiveTimeSeconds = null;
        if (acm.TargetType == AchievementTargetType.ActiveTime)
        {
            var seconds = acm.GetTargetSecondsSpent();
            targetActiveTimeSeconds = (int)Math.Round(seconds);
        }

        double? targetValue = null;
        if (acm.TargetType == AchievementTargetType.Value ||
            acm.TargetType == AchievementTargetType.Steps ||
            acm.TargetType == AchievementTargetType.Achievements ||
            acm.TargetType == AchievementTargetType.Custom)
        {
            targetValue = acm.TargetValue;
        }

        string? rangeUnitText = null;
        int? rangeAmount = null;
        if (acm.CompletionType == AchievementCompletionType.Range)
        {
            rangeUnitText = acm.RangeUnit.ToString();
            rangeAmount = acm.RangeAmount;
        }

        var deadlineStartText = SerializeNullableStoredDateTime(acm.DeadlineStart);
        var deadlineText = SerializeNullableStoredDateTime(acm.Deadline);
        var finalizedAtText = SerializeNullableStoredDateTime(acm.FinalizedAt);
        var frozenCurrentValue = acm.FrozenCurrentValue;
        var lastEarnedAtText = acm.LastEarnedAt.HasValue
            ? StrictTimeSerializer.SerializeUtcInstant(ToUtcInstantForWrite(acm.LastEarnedAt.Value))
            : null;

        var trophyUrls = acm.Trophies.Count == 0
            ? ""
            : string.Join("\n",
                acm.Trophies
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim()));

        if (acm.Id == 0)
        {
            await _context.Db.ExecuteAsync(
                @"INSERT INTO AchievementCard
                  (CardID,
                   Status,
                   Description,
                   TargetType,
                   DifficultyLevel,
                   CreatedDate,
                   LastEarnedAt,
                   TargetActiveTimeInSeconds,
                   TargetValue,
                   ScCardStepID,
                   CompletionType,
                   RangeUnit,
                   RangeAmount,
                   DeadlineStart,
                   Deadline,
                   FinalizedAt,
                   FrozenCurrentValue,
                   TrophyURLs,
                   IsPinned)
                  VALUES
                  (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                cardId,
                acm.Status ?? "",
                acm.Description ?? "",
                targetTypeText,
                difficultyText,
                SerializeStoredDateTime(acm.CreatedDate == default ? now : acm.CreatedDate),
                lastEarnedAtText,
                targetActiveTimeSeconds,
                targetValue,
                null,
                completionTypeText,
                rangeUnitText,
                rangeAmount,
                deadlineStartText,
                deadlineText,
                finalizedAtText,
                frozenCurrentValue,
                trophyUrls,
                acm.IsPinned ? 1 : 0);

            acm.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }
        else
        {
            await _context.Db.ExecuteAsync(
                @"UPDATE AchievementCard
                  SET Status                   = ?,
                      Description              = ?,
                      TargetType               = ?,
                      DifficultyLevel          = ?,
                      LastEarnedAt             = ?,
                      TargetActiveTimeInSeconds= ?,
                      TargetValue              = ?,
                      ScCardStepID             = ?,
                      CompletionType           = ?,
                      RangeUnit                = ?,
                      RangeAmount              = ?,
                      DeadlineStart            = ?,
                      Deadline                 = ?,
                      FinalizedAt              = ?,
                      FrozenCurrentValue       = ?,
                      TrophyURLs               = ?,
                      IsPinned                 = ?
                  WHERE CardID = ?;",
                acm.Status ?? "",
                acm.Description ?? "",
                targetTypeText,
                difficultyText,
                lastEarnedAtText,
                targetActiveTimeSeconds,
                targetValue,
                null,
                completionTypeText,
                rangeUnitText,
                rangeAmount,
                deadlineStartText,
                deadlineText,
                finalizedAtText,
                frozenCurrentValue,
                trophyUrls,
                acm.IsPinned ? 1 : 0,
                cardId);
        }
    }

    public async Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt)
    {
        await _context.InitializeAsync();

        var earnedIso = StrictTimeSerializer.SerializeUtcInstant(ToUtcInstantForWrite(earnedAt));

        await _context.RunInTransactionAsync(tran =>
        {
            tran.Execute(
                @"UPDATE AchievementCard
                  SET LastEarnedAt = ?
                  WHERE AchievementCardID = ?;",
                earnedIso,
                achievementId);

            TryAwardRandomTrophyInTransaction(tran, achievementId, earnedIso);
        });
    }

    public async Task DeleteAchievementCardModelAsync(AchievementCardModel model)
    {
        await _context.InitializeAsync();

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (model.Id == 0)
            return;

        var cardIds = await _context.Db.QueryScalarsAsync<long>(
            "SELECT CardID FROM AchievementCard WHERE AchievementCardID = ? LIMIT 1;",
            model.Id);

        var cardId = cardIds.FirstOrDefault();
        if (cardId == 0)
            return;

        await _context.Db.ExecuteAsync("DELETE FROM Card WHERE CardID = ?;", cardId);
    }

    public async Task DeleteAchievementTrophyAsync(int trophyId)
    {
        await _context.InitializeAsync();

        await _context.Db.ExecuteAsync(
            @"DELETE FROM AchievementTrophy
              WHERE TrophyID = ?;",
            trophyId);
    }

    public async Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        return await FinalizeDeadlineAchievementIfNeededAsync(card);
    }

    public async Task FinalizeDeadlineAchievementCompletedAsync(long achievementId, double frozenCurrentValue, DateTime finalizedAtLocal)
    {
        await _context.InitializeAsync();

        var finalizedIso = SerializeStoredDateTime(finalizedAtLocal);

        await _context.RunInTransactionAsync(tran =>
        {
            tran.Execute(
                @"UPDATE AchievementCard
                  SET Status = ?,
                      FinalizedAt = ?,
                      FrozenCurrentValue = ?,
                      LastEarnedAt = ?
                  WHERE AchievementCardID = ?;",
                "Completed",
                finalizedIso,
                frozenCurrentValue,
                finalizedIso,
                achievementId);

            TryAwardRandomTrophyInTransaction(tran, achievementId, finalizedIso);
        });
    }

    public async Task FinalizeDeadlineAchievementFailedAsync(long achievementId, double frozenCurrentValue, DateTime finalizedAtLocal)
    {
        await _context.InitializeAsync();

        var finalizedIso = SerializeStoredDateTime(finalizedAtLocal);

        await _context.RunInTransactionAsync(tran =>
        {
            tran.Execute(
                @"UPDATE AchievementCard
                  SET Status = ?,
                      FinalizedAt = ?,
                      FrozenCurrentValue = ?
                  WHERE AchievementCardID = ?;",
                "Failed",
                finalizedIso,
                frozenCurrentValue,
                achievementId);
        });
    }

    private AchievementCardModel MapAchievementRowToModel(AchievementCardJoinedRow row)
    {
        var difficulty = AchievementDifficultyLevels.Easy;
        if (!string.IsNullOrWhiteSpace(row.DifficultyLevel))
            Enum.TryParse(row.DifficultyLevel, out difficulty);

        var targetType = AchievementTargetType.ActiveTime;
        if (!string.IsNullOrWhiteSpace(row.TargetType))
            Enum.TryParse(row.TargetType, out targetType);

        var completionType = AchievementCompletionType.Range;
        if (!string.IsNullOrWhiteSpace(row.CompletionType))
            Enum.TryParse(row.CompletionType, out completionType);

        var rangeUnit = AchievementRangeUnit.Days;
        if (!string.IsNullOrWhiteSpace(row.RangeUnit))
            Enum.TryParse(row.RangeUnit, out rangeUnit);

        var model = new AchievementCardModel
        {
            Id = row.AchievementCardID,
            CardID = row.CardID,
            DisplayOrder = row.DisplayOrder,
            Title = row.Title ?? "",
            Tags = row.Tags ?? "",
            Status = row.Status ?? "",
            Description = row.Description ?? "",
            Difficulty = difficulty,
            TargetType = targetType,
            CompletionType = completionType,
            RangeUnit = rangeUnit,
            CreatedDate = !string.IsNullOrWhiteSpace(row.CreatedDate)
                ? ReadStoredDateTime(row.CreatedDate)
                : _clock.UtcNow,
            RangeAmount = row.RangeAmount ?? 0,
            TargetValue = row.TargetValue ?? 0,
            FrozenCurrentValue = row.FrozenCurrentValue,
            IsPinned = row.IsPinned == 1
        };

        if (row.TargetActiveTimeInSeconds.HasValue && row.TargetActiveTimeInSeconds.Value > 0)
        {
            var ts = TimeSpan.FromSeconds(row.TargetActiveTimeInSeconds.Value);
            var hours = (int)ts.TotalHours;
            model.ActiveTimeTargetText = $"{hours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        if (!string.IsNullOrWhiteSpace(row.CreatedDate))
            model.CreatedDate = ReadStoredDateTime(row.CreatedDate);

        if (!string.IsNullOrWhiteSpace(row.DeadlineStart))
            model.DeadlineStart = ReadStoredDateTime(row.DeadlineStart);

        if (!string.IsNullOrWhiteSpace(row.Deadline))
            model.Deadline = ReadStoredDateTime(row.Deadline);

        if (!string.IsNullOrWhiteSpace(row.LastEarnedAt))
            model.LastEarnedAt = ParseInstantUtc(row.LastEarnedAt);

        if (!string.IsNullOrWhiteSpace(row.FinalizedAt))
            model.FinalizedAt = ReadStoredDateTime(row.FinalizedAt);

        if (!string.IsNullOrWhiteSpace(row.TrophyURLs))
        {
            foreach (var t in row.TrophyURLs
                         .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                model.Trophies.Add(t);
            }
        }

        return model;
    }

    private async Task<Dictionary<string, TimeValueAchievementEvaluator>> BuildEvaluatorsByTag(IEnumerable<AchievementCardModel> cards)
    {
        if (cards == null)
            throw new ArgumentNullException(nameof(cards));

        var result = new Dictionary<string, TimeValueAchievementEvaluator>();

        foreach (var group in cards.GroupBy(c => c.Tags ?? string.Empty))
        {
            var evaluationTasks = group.Select(CreateEvaluation);
            var evaluations = await Task.WhenAll(evaluationTasks);

            result[group.Key] = new TimeValueAchievementEvaluator
            {
                Evaluations = evaluations.ToList()
            };
        }

        return result;
    }

    private async Task<TimeValueAchievementEvaluation> CreateEvaluation(AchievementCardModel card)
    {
        var now = _clock.LocalNow;

        if (card.CompletionType == AchievementCompletionType.Deadline &&
            card.FinalizedAt.HasValue)
        {
            return new TimeValueAchievementEvaluation
            {
                AchievementCard = card,
                CurrentValue = card.FrozenCurrentValue ?? 0
            };
        }

        if (!card.TryGetEvaluationWindow(now, out var windowStart, out var windowEnd))
        {
            return new TimeValueAchievementEvaluation
            {
                AchievementCard = card,
                CurrentValue = 0
            };
        }

        var summary = await GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd);

        return card.TargetType switch
        {
            AchievementTargetType.ActiveTime => new TimeValueAchievementEvaluation
            {
                AchievementCard = card,
                CurrentValue = summary.CurrentTotalActiveTimeInSeconds
            },
            AchievementTargetType.Value => new TimeValueAchievementEvaluation
            {
                AchievementCard = card,
                CurrentValue = summary.CurrentValue
            },
            _ => throw new NotSupportedException(
                $"Unsupported TargetType '{card.TargetType}' for AchievementCard '{card}'.")
        };
    }

    private async Task<TagValueSummaryRow> GetTagValueSummaryAsync(string tagName, DateTime rangeStart, DateTime rangeEnd)
    {
        await _context.InitializeAsync();

        var rangeUtc = ToInstantQueryUtcRange(rangeStart, rangeEnd);
        var taggedCards = await _context.Db.QueryAsync<CardIdOnlyRow>(
            @"SELECT c.CardID AS CardID
              FROM Card c
              WHERE ',' || REPLACE(c.Tags, ' ', '') || ','
                    LIKE '%,' || REPLACE(?, ' ', '') || ',%';",
            tagName);

        var cardIds = taggedCards
            .Select(c => c.CardID)
            .Distinct()
            .ToList();

        if (cardIds.Count == 0)
            return new TagValueSummaryRow();

        var placeholders = string.Join(", ", cardIds.Select(_ => "?"));
        var args = cardIds.Cast<object>().ToArray();

        var activityRows = await _context.Db.QueryAsync<ActivityRow>(
            $@"SELECT
                   ActivityID       AS ActivityID,
                   CardID           AS CardID,
                   Start            AS Start,
                   ""End""          AS End,
                   ValueRateName    AS ValueRateName,
                   ValuePerMinute   AS ValuePerMinute
               FROM Activity
               WHERE CardID IN ({placeholders})
               ORDER BY CardID, Start;",
            args);

        double activityValue = 0;
        double totalActiveSeconds = 0;

        foreach (var activity in activityRows.Select(ToActivityModel))
        {
            var (startUtc, endUtc) = GetActivityIntervalUtc(activity, validateOrder: false);
            var clippedStart = startUtc > rangeUtc.StartUtc ? startUtc : rangeUtc.StartUtc;
            var rawEnd = endUtc ?? rangeUtc.EndUtc;
            var clippedEnd = rawEnd < rangeUtc.EndUtc ? rawEnd : rangeUtc.EndUtc;

            if (clippedEnd <= clippedStart)
                continue;

            var seconds = (clippedEnd - clippedStart).TotalSeconds;
            totalActiveSeconds += seconds;
            activityValue += (seconds / 60.0) * activity.ValuePerMinute;
        }

        var repRows = await _context.Db.QueryAsync<ScCardStepRepRow>(
            $@"SELECT
                   rep.ScCardStepID AS ScCardStepID,
                   rep.TimeStamp    AS TimeStamp,
                   rep.StepValue    AS StepValue
               FROM ScCard sc
               JOIN ScCardStep st ON st.ScCardID = sc.ScCardID
               JOIN ScCardStepRep rep ON rep.ScCardStepID = st.ScCardStepID
               WHERE sc.CardID IN ({placeholders});",
            args);

        var stepValue = repRows
            .Where(r => InstantFallsInUtcRange(ParseInstantUtc(r.TimeStamp), rangeUtc))
            .Sum(r => r.StepValue);

        var missionRows = await _context.Db.QueryAsync<MissionCompletionValueRow>(
            $@"SELECT CompletedDate AS CompletedDate, Value AS Value
               FROM MissionCard
               WHERE CardID IN ({placeholders})
                 AND CompletedDate IS NOT NULL;",
            args);

        var missionValue = missionRows
            .Where(m => !string.IsNullOrWhiteSpace(m.CompletedDate))
            .Where(m => InstantFallsInUtcRange(ParseInstantUtc(m.CompletedDate!), rangeUtc))
            .Sum(m => m.Value);

        return new TagValueSummaryRow
        {
            CurrentValue = activityValue + stepValue + missionValue,
            CurrentTotalActiveTimeInSeconds = totalActiveSeconds
        };
    }

    private async Task<AchievementCardModel> ReloadAchievementAfterFinalizationAsync(long achievementId)
    {
        return await GetAchievementCardModelDataAsync((int)achievementId);
    }

    private async Task<AchievementCardModel> FinalizeDeadlineAchievementIfNeededAsync(AchievementCardModel card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        if (card.CompletionType != AchievementCompletionType.Deadline)
            return card;

        if (card.FinalizedAt.HasValue)
            return card;

        var now = _clock.LocalNow;

        if (!card.TryGetEvaluationWindow(now, out var windowStart, out var windowEnd))
            return card;

        double currentValue;

        switch (card.TargetType)
        {
            case AchievementTargetType.Value:
                {
                    var summary = await GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd);
                    currentValue = summary.CurrentValue;
                    break;
                }

            case AchievementTargetType.ActiveTime:
                {
                    var summary = await GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd);
                    currentValue = summary.CurrentTotalActiveTimeInSeconds;
                    break;
                }

            default:
                return card;
        }

        var transition = GetDeadlineTransitionResult(card, currentValue, now);

        switch (transition)
        {
            case DeadlineTransitionResult.Complete:
                await FinalizeDeadlineAchievementCompletedAsync(card.Id, currentValue, now);
                return await ReloadAchievementAfterFinalizationAsync(card.Id);

            case DeadlineTransitionResult.Fail:
                await FinalizeDeadlineAchievementFailedAsync(card.Id, currentValue, now);
                return await ReloadAchievementAfterFinalizationAsync(card.Id);

            default:
                return card;
        }
    }

    private async Task<List<AchievementCardModel>> FinalizeDeadlineAchievementsIfNeededAsync(IEnumerable<AchievementCardModel> cards)
    {
        if (cards == null)
            return new List<AchievementCardModel>();

        var result = new List<AchievementCardModel>();

        foreach (var card in cards)
        {
            var updated = await FinalizeDeadlineAchievementIfNeededAsync(card);
            result.Add(updated);
        }

        return result;
    }

    private void TryAwardRandomTrophyInTransaction(SQLiteConnection tran, long achievementId, string earnedIso)
    {
        var folder = _achievementTrophiesPathFactory((int)achievementId);
        if (!Directory.Exists(folder))
            return;

        var earnableFileNames = Directory.EnumerateFiles(folder)
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f!)
            .ToList();

        if (earnableFileNames.Count == 0)
            return;

        var earned = tran.Query<AchievementTrophyRow>(
                @"SELECT TrophyID, AchievementCardID, Title, EarnedOn, ImageSource
                  FROM AchievementTrophy
                  WHERE AchievementCardID = ?;",
                achievementId)
            .Select(x => x.ImageSource)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var earnableSet = earnableFileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = earnableFileNames
            .Where(f => !earned.Contains(f))
            .Where(f => PrerequisiteSatisfied(f, earnableSet, earned))
            .ToList();

        if (candidates.Count == 0)
            return;

        var idx = RandomNumberGenerator.GetInt32(candidates.Count);
        var chosen = candidates[idx];
        var title = Path.GetFileNameWithoutExtension(chosen) ?? "";

        tran.Execute(
            @"INSERT INTO AchievementTrophy (AchievementCardID, Title, EarnedOn, ImageSource)
              VALUES (?, ?, ?, ?);",
            achievementId,
            title,
            earnedIso,
            chosen);
    }

    private static bool PrerequisiteSatisfied(string fileName, HashSet<string> earnableFiles, HashSet<string> earnedFiles)
    {
        string? prerequisite = null;

        for (var i = 0; i < fileName.Length; i++)
        {
            if (fileName[i] != '_')
                continue;

            var suffix = fileName[(i + 1)..];
            if (string.IsNullOrWhiteSpace(suffix))
                continue;

            if (!earnableFiles.Contains(suffix))
                continue;

            if (prerequisite == null || suffix.Length > prerequisite.Length)
                prerequisite = suffix;
        }

        return prerequisite == null || earnedFiles.Contains(prerequisite);
    }

    private static void ValidateAchievementForPersistence(AchievementCardModel acm)
    {
        if (acm == null)
            throw new ArgumentNullException(nameof(acm));

        if (acm.CompletionType != AchievementCompletionType.Deadline)
            return;

        if (!acm.Deadline.HasValue)
            throw new InvalidOperationException("Deadline achievements must have a deadline.");

        var effectiveStart = acm.DeadlineStart ?? acm.CreatedDate;

        if (effectiveStart > acm.Deadline.Value)
            throw new InvalidOperationException("DeadlineStart cannot be later than Deadline.");
    }

    private static bool ShouldKeepLoadedAfterFinalization(AchievementCardModel card, DateTime now)
    {
        if (card == null)
            return false;

        if (card.CompletionType != AchievementCompletionType.Deadline)
            return true;

        if (!card.FinalizedAt.HasValue)
            return true;

        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);

        return card.FinalizedAt.Value >= todayStart && card.FinalizedAt.Value < tomorrowStart;
    }

    private static DeadlineTransitionResult GetDeadlineTransitionResult(AchievementCardModel card, double currentValue, DateTime now)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        if (card.CompletionType != AchievementCompletionType.Deadline)
            return DeadlineTransitionResult.None;

        if (card.FinalizedAt.HasValue)
            return DeadlineTransitionResult.None;

        if (!card.TryGetEvaluationWindow(now, out var start, out _))
            return DeadlineTransitionResult.None;

        if (start > now)
            return DeadlineTransitionResult.None;

        if (currentValue >= card.TargetValue && card.TargetValue > 0)
            return DeadlineTransitionResult.Complete;

        if (card.Deadline.HasValue && now > card.Deadline.Value)
            return DeadlineTransitionResult.Fail;

        return DeadlineTransitionResult.None;
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

    private UtcDateTimeRange ToInstantQueryUtcRange(DateTime rangeStart, DateTime rangeEnd)
    {
        return new UtcDateTimeRange(
            ToUtcInstantForWrite(rangeStart),
            ToUtcInstantForWrite(rangeEnd));
    }

    private string SerializeStoredDateTime(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? StrictTimeSerializer.SerializeUtcInstant(value)
            : StrictTimeSerializer.SerializeLocalDateTime(value);
    }

    private string? SerializeNullableStoredDateTime(DateTime? value)
    {
        return value.HasValue ? SerializeStoredDateTime(value.Value) : null;
    }

    private DateTime ReadStoredDateTime(string value)
    {
        if (StrictTimeSerializer.HasExplicitUtcOrOffset(value))
            return ParseInstantUtc(value);

        return LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime;
    }

    private static bool InstantFallsInUtcRange(DateTime utcInstant, UtcDateTimeRange range)
    {
        utcInstant = StrictTimeSerializer.RequireUtcInstant(utcInstant, nameof(utcInstant));
        return utcInstant >= range.StartUtc && utcInstant <= range.EndUtc;
    }

    private ActivityModel ToActivityModel(ActivityRow row)
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
            RateName = row.ValueRateName ?? "",
            ValuePerMinute = row.ValuePerMinute
        };
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

    private enum DeadlineTransitionResult
    {
        None = 0,
        Complete = 1,
        Fail = 2
    }

    private sealed class AchievementCardJoinedRow
    {
        public int AchievementCardID { get; set; }
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? TargetType { get; set; }
        public string? DifficultyLevel { get; set; }
        public string CreatedDate { get; set; } = "";
        public string? LastEarnedAt { get; set; }
        public int? TargetActiveTimeInSeconds { get; set; }
        public double? TargetValue { get; set; }
        public int? ScCardStepID { get; set; }
        public string? CompletionType { get; set; }
        public string? RangeUnit { get; set; }
        public int? RangeAmount { get; set; }
        public string? DeadlineStart { get; set; }
        public string? Deadline { get; set; }
        public string? FinalizedAt { get; set; }
        public double? FrozenCurrentValue { get; set; }
        public string? TrophyURLs { get; set; }
        public int IsPinned { get; set; }
    }

    private sealed class TrophyRow
    {
        public int Id { get; set; }
        public int AchievementId { get; set; }
        public string Title { get; set; } = "";
        public string EarnedOn { get; set; } = "";
        public string ImageSource { get; set; } = "";
    }

    public sealed class TagValueSummaryRow
    {
        public double CurrentValue { get; set; }
        public double CurrentTotalActiveTimeInSeconds { get; set; }
    }

    private sealed class CardIdOnlyRow
    {
        public long CardID { get; set; }
    }

    private sealed class MissionCompletionValueRow
    {
        public string? CompletedDate { get; set; }
        public double Value { get; set; }
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

    private sealed class ScCardStepRepRow
    {
        public int ScCardStepID { get; set; }
        public string TimeStamp { get; set; } = "";
        public double StepValue { get; set; }
    }

    [Table("AchievementTrophy")]
    public sealed class AchievementTrophyRow
    {
        [PrimaryKey, AutoIncrement]
        public long TrophyID { get; set; }

        [Indexed]
        public long AchievementCardID { get; set; }

        public string Title { get; set; } = "";
        public string EarnedOn { get; set; } = "";
        public string ImageSource { get; set; } = "";
    }
}
