using System.Globalization;
using System.Security.Cryptography;
using Points.Evaluators;
using Points.Global;
using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Repositories.Interfaces;
using SQLite;

namespace Points.Services.Sqlite
{
    public sealed partial class AchievementRepository : SqliteRepositoryBase, IAchievementRepository
    {
        private readonly IAchievementCardLookupRepository _achievementCardLookupRepository;
        private readonly IAchievementEvaluationService _achievementEvaluationService;

        public AchievementRepository(
            ISqliteConnectionManager connectionManager,
            IAchievementCardLookupRepository achievementCardLookupRepository,
            IAchievementEvaluationService achievementEvaluationService)
            : base(connectionManager)
        {
            _achievementCardLookupRepository = achievementCardLookupRepository ?? throw new ArgumentNullException(nameof(achievementCardLookupRepository));
            _achievementEvaluationService = achievementEvaluationService ?? throw new ArgumentNullException(nameof(achievementEvaluationService));
        }

        public async Task DeleteAchievementCardModelAsync(AchievementCardModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            await EnsureInitializedAsync().ConfigureAwait(false);

            if (model.Id == 0)
                return;

            var cardIds = await Db.QueryScalarsAsync<long>(
                @"SELECT CardID
                  FROM AchievementCard
                  WHERE AchievementCardID = ?
                  LIMIT 1;",
                model.Id).ConfigureAwait(false);

            var cardId = cardIds.FirstOrDefault();
            if (cardId == 0)
                return;

            await Db.ExecuteAsync(
                @"DELETE FROM Card
                  WHERE CardID = ?;",
                cardId).ConfigureAwait(false);
        }

        public async Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var earnedIso = earnedAt.Kind == DateTimeKind.Utc
                ? earnedAt.ToString("o", CultureInfo.InvariantCulture)
                : earnedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

            await Db.RunInTransactionAsync(tran =>
            {
                tran.Execute(
                    @"UPDATE AchievementCard
                      SET LastEarnedAt = ?
                      WHERE AchievementCardID = ?;",
                    earnedIso,
                    achievementId);

                TryAwardRandomTrophyInTransaction(tran, achievementId, earnedIso);
            }).ConfigureAwait(false);
        }

        public async Task DeleteAchievementTrophyAsync(int trophyId)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            await Db.ExecuteAsync(
                @"DELETE FROM AchievementTrophy
                  WHERE TrophyID = ?;",
                trophyId).ConfigureAwait(false);
        }

        public async Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(List<TimeValueAchievementEvaluator> timeValueAchievementEvaluators)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            if (timeValueAchievementEvaluators == null)
                return new List<TimeValueAchievementEvaluator>();

            var input = timeValueAchievementEvaluators.ToList();
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
                    var card = await _achievementCardLookupRepository
                        .GetAchievementCardByIdAsync(id)
                        .ConfigureAwait(false);

                    return await _achievementEvaluationService
                        .CreateEvaluationAsync(card)
                        .ConfigureAwait(false);
                });

                var evaluations = await Task.WhenAll(tasks).ConfigureAwait(false);
                newEvaluator.Evaluations = evaluations.ToList();

                refreshed.Add(newEvaluator);
            }

            return refreshed;
        }

        public async Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card)
        {
            ArgumentNullException.ThrowIfNull(card);

            await EnsureInitializedAsync().ConfigureAwait(false);
            return await FinalizeDeadlineAchievementIfNeededAsync(card).ConfigureAwait(false);
        }

        private async Task<AchievementCardModel> FinalizeDeadlineAchievementIfNeededAsync(AchievementCardModel card)
        {
            if (card.CompletionType != AchievementCompletionType.Deadline)
                return card;

            if (card.FinalizedAt.HasValue)
                return card;

            var now = DateTime.Now;

            if (!card.TryGetEvaluationWindow(now, out var windowStart, out var windowEnd))
                return card;

            double currentValue;

            switch (card.GoalType)
            {
                case AchievementGoalType.Value:
                    {
                        var summary = await _achievementEvaluationService
                            .GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd)
                            .ConfigureAwait(false);

                        currentValue = summary.CurrentValue;
                        break;
                    }

                case AchievementGoalType.ActiveTime:
                    {
                        var summary = await _achievementEvaluationService
                            .GetTagValueSummaryAsync(card.Tags, windowStart, windowEnd)
                            .ConfigureAwait(false);

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
                    await FinalizeDeadlineAchievementCompletedAsync(card.Id, currentValue, now).ConfigureAwait(false);
                    return await ReloadAchievementAfterFinalizationAsync(card.Id).ConfigureAwait(false);

                case DeadlineTransitionResult.Fail:
                    await FinalizeDeadlineAchievementFailedAsync(card.Id, currentValue, now).ConfigureAwait(false);
                    return await ReloadAchievementAfterFinalizationAsync(card.Id).ConfigureAwait(false);

                default:
                    return card;
            }
        }

        private async Task FinalizeDeadlineAchievementCompletedAsync(long achievementId, double frozenCurrentValue, DateTime finalizedAtLocal)
        {
            var finalizedIso = finalizedAtLocal.ToString("o", CultureInfo.InvariantCulture);

            await Db.RunInTransactionAsync(tran =>
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
            }).ConfigureAwait(false);
        }

        private async Task FinalizeDeadlineAchievementFailedAsync(long achievementId, double frozenCurrentValue, DateTime finalizedAtLocal)
        {
            var finalizedIso = finalizedAtLocal.ToString("o", CultureInfo.InvariantCulture);

            await Db.RunInTransactionAsync(tran =>
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
            }).ConfigureAwait(false);
        }

        private Task<AchievementCardModel> ReloadAchievementAfterFinalizationAsync(long achievementId)
        {
            return _achievementCardLookupRepository.GetAchievementCardByIdAsync((int)achievementId);
        }

        private static DeadlineTransitionResult GetDeadlineTransitionResult(AchievementCardModel card, double currentValue, DateTime now)
        {
            if (card.CompletionType != AchievementCompletionType.Deadline)
                return DeadlineTransitionResult.None;

            if (card.FinalizedAt.HasValue)
                return DeadlineTransitionResult.None;

            if (!card.TryGetEvaluationWindow(now, out var start, out var end))
                return DeadlineTransitionResult.None;

            if (start > now)
                return DeadlineTransitionResult.None;

            if (currentValue >= card.TargetValue && card.TargetValue > 0)
                return DeadlineTransitionResult.Complete;

            if (card.Deadline.HasValue && now > card.Deadline.Value)
                return DeadlineTransitionResult.Fail;

            return DeadlineTransitionResult.None;
        }

        private void TryAwardRandomTrophyInTransaction(SQLiteConnection tran, long achievementId, string earnedIso)
        {
            var folder = AppPaths.GetAchievementTrophiesPath((int)achievementId);
            if (!Directory.Exists(folder))
                return;

            var earnableFileNames = Directory.EnumerateFiles(folder)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Cast<string>()
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

            var title = Path.GetFileNameWithoutExtension(chosen) ?? string.Empty;

            tran.Execute(
                @"INSERT INTO AchievementTrophy (
                      AchievementCardID,
                      Title,
                      EarnedOn,
                      ImageSource
                  )
                  VALUES (?, ?, ?, ?);",
                achievementId,
                title,
                earnedIso,
                chosen);
        }

        private static bool PrerequisiteSatisfied(string fileName, HashSet<string> earnableSet, HashSet<string> earnedSet)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(stem))
                return true;

            var lastDash = stem.LastIndexOf('-');
            if (lastDash <= 0)
                return true;

            var prefix = stem[..lastDash];
            var prerequisiteCandidate = $"{prefix}.png";

            if (!earnableSet.Contains(prerequisiteCandidate))
                return true;

            return earnedSet.Contains(prerequisiteCandidate);
        }
    }
}