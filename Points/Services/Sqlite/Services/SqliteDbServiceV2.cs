using Points.Evaluators;
using Points.Global;
using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Providers;
using Points.Services.Sqlite.Repositories;
using Points.Services.Sqlite.Repositories.Interfaces;
using Points.Services.Sqlite.Services.Interfaces;
using SQLite;

namespace Points.Services.Sqlite.Services
{
    /// <summary>
    /// V2 façade over the SQLite persistence layer.
    /// 
    /// Design goals:
    /// 1. Preserve the exact IDbService contract consumed by the application layer.
    /// 2. Keep the application layer unaware of repositories/sub-services.
    /// 3. Move lifecycle, schema sync, reads, writes and domain-specific persistence
    ///    into focused collaborators that are easier to debug and test.
    /// </summary>
    public sealed class SqliteDbServiceV2 : IDbService
    {
        private readonly ISqliteConnectionManager _connectionManager;
        private readonly ISqliteSchemaManager _schemaManager;

        private readonly IHomeSeedReadRepository _homeSeedReadRepository;
        private readonly ICardReadRepository _cardReadRepository;
        private readonly ICardWriteRepository _cardWriteRepository;
        private readonly IActivityRepository _activityRepository;
        private readonly IAchievementRepository _achievementRepository;
        private readonly ILockRepository _lockRepository;
        private readonly IPlannerRepository _plannerRepository;
        private readonly IShortcutRepository _shortcutRepository;
        private readonly IReportRepository _reportRepository;

        /// <summary>
        /// Default constructor for DI convenience.
        /// Internals are composed inside the SQLite layer so the app layer still depends only on IDbService.
        /// </summary>
        public SqliteDbServiceV2() : this(new SqliteConnectionManager(AppPaths.DatabasePath), new SqliteSchemaManager(new PointsSchemaProvider()), repositories: null)
        {
        }

        /// <summary>
        /// Main constructor used for testability and future internal composition.
        /// </summary>
        public SqliteDbServiceV2(ISqliteConnectionManager connectionManager, ISqliteSchemaManager schemaManager, SqliteDbServiceV2Repositories? repositories = null)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _schemaManager = schemaManager ?? throw new ArgumentNullException(nameof(schemaManager));

            repositories ??= BuildDefaultRepositories(_connectionManager);

            _homeSeedReadRepository = repositories.HomeSeedReadRepository;
            _cardReadRepository = repositories.CardReadRepository;
            _cardWriteRepository = repositories.CardWriteRepository;
            _activityRepository = repositories.ActivityRepository;
            _achievementRepository = repositories.AchievementRepository;
            _lockRepository = repositories.LockRepository;
            _plannerRepository = repositories.PlannerRepository;
            _shortcutRepository = repositories.ShortcutRepository;
            _reportRepository = repositories.ReportRepository;
        }

        /// <summary>
        /// Exposed for internal consumers that may still need direct DB access while the refactor is in progress.
        /// Prefer repositories over using this directly.
        /// </summary>
        public SQLiteAsyncConnection Db => _connectionManager.Db;


        #region Initialization / Lifecycle

        public async Task InitializeAsync()
        {
            await _connectionManager.InitializeAsync().ConfigureAwait(false);
            await _schemaManager.EnsureSchemaAsync(_connectionManager.Db).ConfigureAwait(false);
        }

        public async Task CloseDatabaseAsync()
        {
            await _connectionManager.CloseAsync().ConfigureAwait(false);
        }

        public async Task ReinitializeDatabaseAsync()
        {
            await _connectionManager.ReinitializeAsync().ConfigureAwait(false);
            await _schemaManager.EnsureSchemaAsync(_connectionManager.Db).ConfigureAwait(false);
        }

        #endregion

        #region Database Maintenance

        public async Task BackupAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            await _schemaManager.EnsureSchemaAsync(_connectionManager.Db).ConfigureAwait(false);
            await _connectionManager.BackupAsync().ConfigureAwait(false);
        }

        public async Task WipeAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            await _connectionManager.WipeAsync().ConfigureAwait(false);
        }

        public async Task RestoreAsync(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new ArgumentException("Backup file path is required.", nameof(backupFilePath));

            await _connectionManager.RestoreAsync(backupFilePath).ConfigureAwait(false);
            await _schemaManager.EnsureSchemaAsync(_connectionManager.Db).ConfigureAwait(false);
        }

        public DateTime? GetLastBackupUtc()
        {
            return _connectionManager.GetLastBackupUtc();
        }

        #endregion

        #region Card Reads

        public async Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _cardReadRepository.GetAchievementCardModelsDataAsync().ConfigureAwait(false);
        }

        public async Task<List<TrophyModel>> GetTrophyModelsDataAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _cardReadRepository.GetTrophyModelsDataAsync().ConfigureAwait(false);
        }

        public async Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _homeSeedReadRepository.GetHomeSeedDataAsync(rangeStart, rangeEnd).ConfigureAwait(false);
        }

        public async Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _cardReadRepository.GetMainQuestModelsDataAsync(rangeStart, rangeEnd).ConfigureAwait(false);
        }

        public async Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _cardReadRepository.GetCardScheduleByIdAsync(scheduleId).ConfigureAwait(false);
        }

        public async Task<string?> GetCardTitleByIdAsync(long cardId)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _cardReadRepository.GetCardTitleByIdAsync(cardId).ConfigureAwait(false);
        }

        #endregion

        #region Card Writes

        public async Task SaveCardModelAsync(ICardModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            await InitializeAsync().ConfigureAwait(false);
            await _cardWriteRepository.SaveCardModelAsync(model).ConfigureAwait(false);
        }

        #endregion

        #region Activities / Time Tracking

        public async Task<ActivityModel?> GetCurrentActiveActivityAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _activityRepository.GetCurrentActiveActivityAsync().ConfigureAwait(false);
        }

        public async Task<ToggleActivityModelResult> ToggleActivityAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _activityRepository
                .ToggleActivityAsync(cardId, utcNow, valueRateName, valuePerMinute)
                .ConfigureAwait(false);
        }

        public async Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue)
        {
            await InitializeAsync().ConfigureAwait(false);
            await _activityRepository.AddRepForStep(scCardStepID, repTime, stepValue).ConfigureAwait(false);
        }

        public async Task<bool> HasActivityOverlapAsync(int excludeActivityId, DateTime candidateStart, DateTime? candidateEnd)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _activityRepository
                .HasActivityOverlapAsync(excludeActivityId, candidateStart, candidateEnd)
                .ConfigureAwait(false);
        }

        public async Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _activityRepository.GetCurrentOpenActivityStartUtcAsync(cardId).ConfigureAwait(false);
        }

        public async Task<DateTime?> GetLastClosedActivityEndUtcAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _activityRepository.GetLastClosedActivityEndUtcAsync().ConfigureAwait(false);
        }

        #endregion

        #region Achievements

        public async Task DeleteAchievementCardModelAsync(AchievementCardModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            await InitializeAsync().ConfigureAwait(false);
            await _achievementRepository.DeleteAchievementCardModelAsync(model).ConfigureAwait(false);
        }

        public async Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt)
        {
            await InitializeAsync().ConfigureAwait(false);
            await _achievementRepository.MarkAchievementEarnedAsync(achievementId, earnedAt).ConfigureAwait(false);
        }

        public async Task DeleteAchievementTrophyAsync(int trophyId)
        {
            await InitializeAsync().ConfigureAwait(false);
            await _achievementRepository.DeleteAchievementTrophyAsync(trophyId).ConfigureAwait(false);
        }

        public async Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(
            List<TimeValueAchievementEvaluator> timeValueAchievementEvaluators)
        {
            ArgumentNullException.ThrowIfNull(timeValueAchievementEvaluators);

            await InitializeAsync().ConfigureAwait(false);
            return await _achievementRepository
                .RefreshEvaluatorsAsync(timeValueAchievementEvaluators)
                .ConfigureAwait(false);
        }

        public async Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card)
        {
            ArgumentNullException.ThrowIfNull(card);

            await InitializeAsync().ConfigureAwait(false);
            return await _achievementRepository.ReevaluateDeadlineAchievementAsync(card).ConfigureAwait(false);
        }

        #endregion

        #region Locks

        public async Task<List<LockModel>> GetLocksForCardAsync(long cardId)
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _lockRepository.GetLocksForCardAsync(cardId).ConfigureAwait(false);
        }

        public async Task SaveLocksForCardAsync(long cardId, List<LockModel> locksToSave)
        {
            ArgumentNullException.ThrowIfNull(locksToSave);

            await InitializeAsync().ConfigureAwait(false);
            await _lockRepository.SaveLocksForCardAsync(cardId, locksToSave).ConfigureAwait(false);
        }

        public async Task DeleteLockModelAsync(LockModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            await InitializeAsync().ConfigureAwait(false);
            await _lockRepository.DeleteLockModelAsync(model).ConfigureAwait(false);
        }

        #endregion

        #region Planner

        public async Task<List<PlannerGoalDetailsModel>> GetPlannerModelsDataAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _plannerRepository.GetPlannerModelsDataAsync().ConfigureAwait(false);
        }

        public async Task SavePlannerModelsDataAsync(List<PlannerGoalDetailsModel> plannerModelsToSave)
        {
            ArgumentNullException.ThrowIfNull(plannerModelsToSave);

            await InitializeAsync().ConfigureAwait(false);
            await _plannerRepository.SavePlannerModelsDataAsync(plannerModelsToSave).ConfigureAwait(false);
        }

        #endregion

        #region Shortcuts

        public async Task<List<ShortcutGroupModel>> GetShortcutGroupsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _shortcutRepository.GetShortcutGroupsAsync().ConfigureAwait(false);
        }

        public async Task<List<ShortcutModel>> GetDashboardShortcutsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _shortcutRepository.GetDashboardShortcutsAsync().ConfigureAwait(false);
        }

        public async Task<ShortcutGroupModel> UpsertShortcutGroupAsync(ShortcutGroupModel group)
        {
            ArgumentNullException.ThrowIfNull(group);

            await InitializeAsync().ConfigureAwait(false);
            return await _shortcutRepository.UpsertShortcutGroupAsync(group).ConfigureAwait(false);
        }

        public async Task<ShortcutModel> SaveShortcutAsync(ShortcutModel shortcut)
        {
            ArgumentNullException.ThrowIfNull(shortcut);

            await InitializeAsync().ConfigureAwait(false);
            return await _shortcutRepository.SaveShortcutAsync(shortcut).ConfigureAwait(false);
        }

        public async Task DeleteShortcutAsync(long shortcutId)
        {
            await InitializeAsync().ConfigureAwait(false);
            await _shortcutRepository.DeleteShortcutAsync(shortcutId).ConfigureAwait(false);
        }

        #endregion

        #region Reports

        public async Task<IReadOnlyList<string>> ExecuteSelectForReportAsync(
            string sql,
            bool includeHeaderRow = true,
            params object?[] args)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL is required.", nameof(sql));

            await InitializeAsync().ConfigureAwait(false);
            return await _reportRepository
                .ExecuteSelectForReportAsync(sql, includeHeaderRow, args)
                .ConfigureAwait(false);
        }

        public async Task UpsertReportAsync(ReportModel report)
        {
            ArgumentNullException.ThrowIfNull(report);

            await InitializeAsync().ConfigureAwait(false);
            await _reportRepository.UpsertReportAsync(report).ConfigureAwait(false);
        }

        public async Task DeleteReportAsync(int reportId)
        {
            await InitializeAsync().ConfigureAwait(false);
            await _reportRepository.DeleteReportAsync(reportId).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ReportModel>> GetReportsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return await _reportRepository.GetReportsAsync().ConfigureAwait(false);
        }

        #endregion

        private static SqliteDbServiceV2Repositories BuildDefaultRepositories(ISqliteConnectionManager connectionManager)
        {
            // Core read dependencies
            var tatReadRepository = new TatReadRepository(connectionManager);
            var scReadRepository = new ScReadRepository(connectionManager);
            var missionReadRepository = new MissionReadRepository(connectionManager);
            var budgetReadRepository = new BudgetReadRepository(connectionManager);
            var trackerReadRepository = new TrackerReadRepository(connectionManager);

            // Achievement dependencies
            var achievementCardLookupRepository = new AchievementCardLookupRepository(connectionManager);
            var achievementEvaluationService = new AchievementEvaluationService(
                connectionManager,
                achievementCardLookupRepository);

            var achievementCardMaterializer = new AchievementCardMaterializer(
                achievementEvaluationService);

            var achievementCardReadRepository = new AchievementCardReadRepository(
                connectionManager,
                achievementCardMaterializer);

            var achievementRepository = new AchievementRepository(
                connectionManager,
                achievementCardLookupRepository,
                achievementEvaluationService);

            // Enrichment services used by HomeSeed
            var achievementEnrichmentService = new AchievementEnrichmentService(
                achievementEvaluationService);

            var lockRepository = new LockRepository(connectionManager);

            var lockEnrichmentService = new LockEnrichmentService(lockRepository);

            // Aggregate readers
            var mainQuestReadRepository = new MainQuestReadRepository(
                tatReadRepository,
                scReadRepository);

            var homeSeedReadRepository = new HomeSeedReadRepository(
                connectionManager,
                mainQuestReadRepository,
                missionReadRepository,
                budgetReadRepository,
                achievementCardReadRepository,
                trackerReadRepository,
                achievementEnrichmentService,
                lockEnrichmentService);

            // Public-facing facade repos
            var cardReadRepository = new CardReadRepository(
                connectionManager,
                tatReadRepository,
                scReadRepository,
                achievementCardMaterializer);

            var cardIdLookupService = new CardIdLookupService(connectionManager);

            var scCardWriteRepository = new ScCardWriteRepository(connectionManager);
            var tatCardWriteRepository = new TatCardWriteRepository(connectionManager);
            var missionCardWriteRepository = new MissionCardWriteRepository(connectionManager);
            var budgetCardWriteRepository = new BudgetCardWriteRepository(connectionManager);
            var achievementCardWriteRepository = new AchievementCardWriteRepository(connectionManager);
            var valueTrackerCardWriteRepository = new ValueTrackerCardWriteRepository(connectionManager);
            var eventTrackerCardWriteRepository = new EventTrackerCardWriteRepository(connectionManager);

            var cardWriteRepository = new CardWriteRepository(
                connectionManager,
                scCardWriteRepository,
                tatCardWriteRepository,
                missionCardWriteRepository,
                budgetCardWriteRepository,
                achievementCardWriteRepository,
                valueTrackerCardWriteRepository,
                eventTrackerCardWriteRepository,
                cardIdLookupService);

            var activityRepository = new ActivityRepository(connectionManager);
            var plannerRepository = new PlannerRepository(connectionManager);
            var shortcutRepository = new ShortcutRepository(connectionManager);
            var reportRepository = new ReportRepository(connectionManager, AppPaths.DatabasePath);

            return new SqliteDbServiceV2Repositories(
                homeSeedReadRepository,
                cardReadRepository,
                cardWriteRepository,
                activityRepository,
                achievementRepository,
                lockRepository,
                plannerRepository,
                shortcutRepository,
                reportRepository);
        }

        /// <summary>
        /// Internal dependency bundle to keep the main constructor readable.
        /// </summary>
        public sealed record SqliteDbServiceV2Repositories(
        IHomeSeedReadRepository HomeSeedReadRepository,
        ICardReadRepository CardReadRepository,
        ICardWriteRepository CardWriteRepository,
        IActivityRepository ActivityRepository,
        IAchievementRepository AchievementRepository,
        ILockRepository LockRepository,
        IPlannerRepository PlannerRepository,
        IShortcutRepository ShortcutRepository,
        IReportRepository ReportRepository);
}