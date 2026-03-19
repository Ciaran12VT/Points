using Points.Evaluators;
using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Repositories.Interfaces;
using Points.Services.Sqlite.Services.Interfaces;

namespace Points.Services.Sqlite.Repositories
{
    /// <summary>
    /// Builds the Home page aggregate/seed object by orchestrating lower-level readers
    /// and enrichers.
    /// </summary>
    public sealed class HomeSeedReadRepository : SqliteRepositoryBase, IHomeSeedReadRepository
    {
        private readonly IMainQuestReadRepository _mainQuestReadRepository;
        private readonly IMissionReadRepository _missionReadRepository;
        private readonly IBudgetReadRepository _budgetReadRepository;
        private readonly IAchievementCardReadRepository _achievementCardReadRepository;
        private readonly ITrackerReadRepository _trackerReadRepository;
        private readonly IAchievementEnrichmentService _achievementEnrichmentService;
        private readonly ILockEnrichmentService _lockEnrichmentService;

        public HomeSeedReadRepository(
            ISqliteConnectionManager connectionManager,
            IMainQuestReadRepository mainQuestReadRepository,
            IMissionReadRepository missionReadRepository,
            IBudgetReadRepository budgetReadRepository,
            IAchievementCardReadRepository achievementCardReadRepository,
            ITrackerReadRepository trackerReadRepository,
            IAchievementEnrichmentService achievementEnrichmentService,
            ILockEnrichmentService lockEnrichmentService)
            : base(connectionManager)
        {
            _mainQuestReadRepository = mainQuestReadRepository ?? throw new ArgumentNullException(nameof(mainQuestReadRepository));
            _missionReadRepository = missionReadRepository ?? throw new ArgumentNullException(nameof(missionReadRepository));
            _budgetReadRepository = budgetReadRepository ?? throw new ArgumentNullException(nameof(budgetReadRepository));
            _achievementCardReadRepository = achievementCardReadRepository ?? throw new ArgumentNullException(nameof(achievementCardReadRepository));
            _trackerReadRepository = trackerReadRepository ?? throw new ArgumentNullException(nameof(trackerReadRepository));
            _achievementEnrichmentService = achievementEnrichmentService ?? throw new ArgumentNullException(nameof(achievementEnrichmentService));
            _lockEnrichmentService = lockEnrichmentService ?? throw new ArgumentNullException(nameof(lockEnrichmentService));
        }

        public async Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var mainQuestCards = await _mainQuestReadRepository
                .GetMainQuestModelsDataAsync(rangeStart, rangeEnd)
                .ConfigureAwait(false);

            // Preserve existing behavior from SqliteDbService:
            // include missions that are incomplete OR completed today or later.
            var missionCards = await _missionReadRepository
                .GetMissionCardModelsDataAsync(MissionReadFilters.HomeSeedActiveOrCompletedToday)
                .ConfigureAwait(false);

            var budgetCards = await _budgetReadRepository
                .GetBudgetCardModelsDataAsync()
                .ConfigureAwait(false);

            var achievements = await _achievementCardReadRepository
                .GetAchievementCardModelsDataAsync()
                .ConfigureAwait(false);

            await _achievementEnrichmentService
                .PopulateAchievementsAsync(achievements, mainQuestCards, missionCards)
                .ConfigureAwait(false);

            await _lockEnrichmentService
                .PopulateLocksAsync(mainQuestCards, missionCards)
                .ConfigureAwait(false);

            var valueTrackers = await _trackerReadRepository
                .GetValueTrackerCardModelsDataAsync()
                .ConfigureAwait(false);

            var eventTrackers = await _trackerReadRepository
                .GetEventTrackerCardModelsDataAsync()
                .ConfigureAwait(false);

            return new HomeSeedData
            {
                MainQuestCards = mainQuestCards,
                MissionCards = missionCards,
                BudgetCards = budgetCards,
                Achievements = achievements,
                ValueTrackers = valueTrackers,
                EventTrackers = eventTrackers
            };
        }
    }

    /// <summary>
    /// Shared mission filter constants so the old inline WHERE clause is not repeated as a magic string.
    /// </summary>
    public static class MissionReadFilters
    {
        /// <summary>
        /// Existing home-seed behavior from SqliteDbService:
        /// include incomplete missions, and also missions completed on or after the local start of today.
        /// </summary>
        public const string HomeSeedActiveOrCompletedToday = "m.CompletedDate IS NULL OR m.CompletedDate >= datetime('now', 'localtime', 'start of day')";
    }
}