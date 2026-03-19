using Points.Models;
using Points.Services.Sqlite.Repositories.Interfaces;

namespace Points.Services.Sqlite
{
    public sealed class CardWriteRepository : SqliteRepositoryBase, ICardWriteRepository
    {
        private readonly IScCardWriteRepository _scCardWriteRepository;
        private readonly ITatCardWriteRepository _tatCardWriteRepository;
        private readonly IMissionCardWriteRepository _missionCardWriteRepository;
        private readonly IBudgetCardWriteRepository _budgetCardWriteRepository;
        private readonly IAchievementCardWriteRepository _achievementCardWriteRepository;
        private readonly IValueTrackerCardWriteRepository _valueTrackerCardWriteRepository;
        private readonly IEventTrackerCardWriteRepository _eventTrackerCardWriteRepository;
        private readonly ICardIdLookupService _cardIdLookupService;

        public CardWriteRepository(
            ISqliteConnectionManager connectionManager,
            IScCardWriteRepository scCardWriteRepository,
            ITatCardWriteRepository tatCardWriteRepository,
            IMissionCardWriteRepository missionCardWriteRepository,
            IBudgetCardWriteRepository budgetCardWriteRepository,
            IAchievementCardWriteRepository achievementCardWriteRepository,
            IValueTrackerCardWriteRepository valueTrackerCardWriteRepository,
            IEventTrackerCardWriteRepository eventTrackerCardWriteRepository,
            ICardIdLookupService cardIdLookupService)
            : base(connectionManager)
        {
            _scCardWriteRepository = scCardWriteRepository ?? throw new ArgumentNullException(nameof(scCardWriteRepository));
            _tatCardWriteRepository = tatCardWriteRepository ?? throw new ArgumentNullException(nameof(tatCardWriteRepository));
            _missionCardWriteRepository = missionCardWriteRepository ?? throw new ArgumentNullException(nameof(missionCardWriteRepository));
            _budgetCardWriteRepository = budgetCardWriteRepository ?? throw new ArgumentNullException(nameof(budgetCardWriteRepository));
            _achievementCardWriteRepository = achievementCardWriteRepository ?? throw new ArgumentNullException(nameof(achievementCardWriteRepository));
            _valueTrackerCardWriteRepository = valueTrackerCardWriteRepository ?? throw new ArgumentNullException(nameof(valueTrackerCardWriteRepository));
            _eventTrackerCardWriteRepository = eventTrackerCardWriteRepository ?? throw new ArgumentNullException(nameof(eventTrackerCardWriteRepository));
            _cardIdLookupService = cardIdLookupService ?? throw new ArgumentNullException(nameof(cardIdLookupService));
        }

        public async Task SaveCardModelAsync(ICardModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            await EnsureInitializedAsync().ConfigureAwait(false);

            var existingCardId = await _cardIdLookupService
                .TryGetCardIdAsync(model)
                .ConfigureAwait(false);

            long cardId;
            if (existingCardId == null)
            {
                await Db.ExecuteAsync(
                    @"INSERT INTO Card (Title, Tags)
                      VALUES (?, ?);",
                    model.Title ?? string.Empty,
                    model.Tags ?? string.Empty)
                    .ConfigureAwait(false);

                cardId = await Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();")
                    .ConfigureAwait(false);
            }
            else
            {
                cardId = existingCardId.Value;

                await Db.ExecuteAsync(
                    @"UPDATE Card
                      SET Title = ?, Tags = ?
                      WHERE CardID = ?;",
                    model.Title ?? string.Empty,
                    model.Tags ?? string.Empty,
                    cardId)
                    .ConfigureAwait(false);
            }

            switch (model)
            {
                case ScCardModel sc:
                    await _scCardWriteRepository.SaveAsync(sc, cardId).ConfigureAwait(false);
                    break;

                case TatCardModel tat:
                    await _tatCardWriteRepository.SaveAsync(tat, cardId).ConfigureAwait(false);
                    break;

                case MissionCardModel mission:
                    await _missionCardWriteRepository.SaveAsync(mission, cardId).ConfigureAwait(false);
                    break;

                case BudgetCardModel budget:
                    await _budgetCardWriteRepository.SaveAsync(budget, cardId).ConfigureAwait(false);
                    break;

                case AchievementCardModel achievement:
                    await _achievementCardWriteRepository.SaveAsync(achievement, cardId).ConfigureAwait(false);
                    break;

                case ValueTrackerCardModel valueTracker:
                    await _valueTrackerCardWriteRepository.SaveAsync(valueTracker, cardId).ConfigureAwait(false);
                    break;

                case EventTrackerCardModel eventTracker:
                    await _eventTrackerCardWriteRepository.SaveAsync(eventTracker, cardId).ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported card model type '{model.GetType().FullName}'.");
            }
        }
    }

    /// <summary>
    /// Resolves the base CardID for an existing subtype row, mirroring the old CheckForCardID logic.
    /// </summary>
    public interface ICardIdLookupService
    {
        Task<long?> TryGetCardIdAsync(ICardModel model);
    }

    public sealed class CardIdLookupService : SqliteRepositoryBase, ICardIdLookupService
    {
        public CardIdLookupService(ISqliteConnectionManager connectionManager)
            : base(connectionManager)
        {
        }

        public async Task<long?> TryGetCardIdAsync(ICardModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            await EnsureInitializedAsync().ConfigureAwait(false);

            if (model.Id <= 0)
                return null;

            return model switch
            {
                ScCardModel => await TryGetCardIdFromSubtypeTableAsync(
                    "ScCard",
                    "ScCardID",
                    model.Id).ConfigureAwait(false),

                TatCardModel => await TryGetCardIdFromSubtypeTableAsync(
                    "TatCard",
                    "TatCardID",
                    model.Id).ConfigureAwait(false),

                MissionCardModel => await TryGetCardIdFromSubtypeTableAsync(
                    "MissionCard",
                    "MissionCardID",
                    model.Id).ConfigureAwait(false),

                BudgetCardModel => await TryGetCardIdFromSubtypeTableAsync(
                    "BudgetCard",
                    "BudgetCardID",
                    model.Id).ConfigureAwait(false),

                AchievementCardModel => await TryGetCardIdFromSubtypeTableAsync(
                    "AchievementCard",
                    "AchievementCardID",
                    model.Id).ConfigureAwait(false),

                ValueTrackerCardModel => await TryGetCardIdFromSubtypeTableAsync(
                    "ValueTrackerCard",
                    "ValueTrackerCardID",
                    model.Id).ConfigureAwait(false),

                EventTrackerCardModel => await TryGetCardIdFromSubtypeTableAsync(
                    "EventTrackerCard",
                    "EventTrackerCardID",
                    model.Id).ConfigureAwait(false),

                _ => throw new NotSupportedException(
                    $"Unsupported card model type '{model.GetType().FullName}'.")
            };
        }

        private async Task<long?> TryGetCardIdFromSubtypeTableAsync(
            string tableName,
            string subtypeIdColumn,
            long subtypeId)
        {
            var sql = $@"
                SELECT CardID
                FROM {tableName}
                WHERE {subtypeIdColumn} = ?
                LIMIT 1;";

            var ids = await Db.QueryScalarsAsync<long>(sql, subtypeId).ConfigureAwait(false);
            var id = ids.FirstOrDefault();

            return id == 0 ? null : id;
        }
    }
}