using System.Globalization;
using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Repositories.Interfaces;

namespace Points.Services.Sqlite
{
    public sealed partial class CardReadRepository : SqliteRepositoryBase, ICardReadRepository
    {
        private readonly ITatReadRepository _tatReadRepository;
        private readonly IScReadRepository _scReadRepository;
        private readonly IAchievementCardMaterializer _achievementCardMaterializer;

        public CardReadRepository(
            ISqliteConnectionManager connectionManager,
            ITatReadRepository tatReadRepository,
            IScReadRepository scReadRepository,
            IAchievementCardMaterializer achievementCardMaterializer)
            : base(connectionManager)
        {
            _tatReadRepository = tatReadRepository ?? throw new ArgumentNullException(nameof(tatReadRepository));
            _scReadRepository = scReadRepository ?? throw new ArgumentNullException(nameof(scReadRepository));
            _achievementCardMaterializer = achievementCardMaterializer ?? throw new ArgumentNullException(nameof(achievementCardMaterializer));
        }

        public async Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT
                    a.AchievementCardID         AS AchievementCardID,
                    a.CardID                    AS CardID,

                    c.Title                     AS Title,
                    c.Tags                      AS Tags,

                    a.Status                    AS Status,
                    a.Description               AS Description,
                    a.GoalType                  AS GoalType,
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
                ORDER BY c.Title;";

            var rows = await Db.QueryAsync<AchievementCardJoinedRow>(sql).ConfigureAwait(false);

            var models = new List<AchievementCardModel>(rows.Count);
            foreach (var row in rows)
            {
                var model = await _achievementCardMaterializer
                    .MaterializeAsync(row)
                    .ConfigureAwait(false);

                models.Add(model);
            }

            return models;
        }

        public async Task<List<TrophyModel>> GetTrophyModelsDataAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT
                    TrophyID           AS Id,
                    AchievementCardID  AS AchievementId,
                    Title,
                    EarnedOn,
                    ImageSource
                FROM AchievementTrophy
                ORDER BY datetime(EarnedOn) DESC;";

            var rows = await Db.QueryAsync<TrophyRow>(sql).ConfigureAwait(false);

            return rows.Select(r => new TrophyModel
            {
                Id = r.Id,
                AchievementId = r.AchievementId,
                Title = r.Title ?? string.Empty,
                EarnedOn = DateTime.Parse(
                    r.EarnedOn,
                    null,
                    DateTimeStyles.RoundtripKind),
                ImageSource = string.IsNullOrWhiteSpace(r.ImageSource)
                    ? "trophy.png"
                    : r.ImageSource
            }).ToList();
        }

        public async Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var tats = await _tatReadRepository
                .GetTatModelsDataAsync(rangeStart, rangeEnd)
                .ConfigureAwait(false);

            var scs = await _scReadRepository
                .GetScModelsDataAsync(rangeStart, rangeEnd)
                .ConfigureAwait(false);

            var mainQuest = new List<IActiveCardModel>(tats.Count + scs.Count);
            mainQuest.AddRange(tats);
            mainQuest.AddRange(scs);

            return mainQuest;
        }

        public async Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT
                    ScheduleId,
                    CardId,
                    IsEnabled,
                    Note,
                    FrequencyType,
                    FrequencyValue,
                    FromDateTime,
                    ToDateTime
                FROM CardSchedule
                WHERE ScheduleId = ?
                LIMIT 1;";

            var row = (await Db.QueryAsync<CardScheduleRow>(sql, scheduleId).ConfigureAwait(false))
                .FirstOrDefault();

            if (row == null)
                return null;

            return new CardSchedule
            {
                ScheduleId = row.ScheduleId,
                CardId = row.CardId,
                IsEnabled = row.IsEnabled != 0,
                Note = row.Note ?? string.Empty,
                FrequencyType = row.FrequencyType,
                FrequencyValue = row.FrequencyValue,
                FromDateTime = ParseIsoDateTime(row.FromDateTime),
                ToDateTime = string.IsNullOrWhiteSpace(row.ToDateTime)
                    ? null
                    : ParseIsoDateTime(row.ToDateTime)
            };
        }

        public async Task<string?> GetCardTitleByIdAsync(long cardId)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT Title
                FROM Card
                WHERE CardID = ?
                LIMIT 1;";

            var rows = await Db.QueryAsync<CardTitleRow>(sql, cardId).ConfigureAwait(false);
            return rows.FirstOrDefault()?.Title;
        }

        private static DateTime ParseIsoDateTime(string value)
        {
            return DateTime.Parse(value, null, DateTimeStyles.RoundtripKind);
        }
    }
}