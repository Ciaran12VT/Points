using Points.Evaluators;
using Points.Global;
using Points.Models;

namespace Points.Services.Sqlite.Interfaces
{
    public interface IDbService :
        IDatabaseInitializationService,
        IDatabaseMaintenanceService,
        ICardReadService,
        ICardWriteService,
        IActivityService,
        IAchievementService,
        ILockService,
        IPlannerService,
        IShortcutService,
        IReportService,
        INotificationLogService
    {
        #region Initialization / Lifecycle

        Task InitializeAsync();
        Task CloseDatabaseAsync();
        Task ReinitializeDatabaseAsync();

        #endregion

        #region Card Reads

        Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync();
        Task<List<TrophyModel>> GetTrophyModelsDataAsync();

        Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd);
        Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);

        Task<List<CardSchedule>> GetEnabledCardSchedulesAsync();
        Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId);
        Task<string?> GetCardTitleByIdAsync(long cardId);

        #endregion

        #region Card Writes

        Task SaveCardModelAsync(ICardModel model);

        #endregion

        #region Activities / Time Tracking

        Task<ActivityModel?> GetCurrentActiveActivityAsync();

        Task<ToggleActivityModelResult> ToggleActivityAsync(long cardId, DateTime utcNow, string valueRateName, double valuePerMinute);

        Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue);

        Task<bool> HasActivityOverlapAsync(int excludeActivityId, DateTime candidateStart, DateTime? candidateEnd);

        Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId);
        Task<DateTime?> GetLastClosedActivityEndUtcAsync();

        #endregion

        #region Achievements

        Task DeleteAchievementCardModelAsync(AchievementCardModel model);
        Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt);
        Task DeleteAchievementTrophyAsync(int trophyId);

        Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(
            List<TimeValueAchievementEvaluator> timeValueAchievementEvaluators);

        Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card);

        #endregion

        #region Locks

        Task<List<LockModel>> GetLocksForCardAsync(long cardId);
        Task SaveLocksForCardAsync(long cardId, List<LockModel> locksToSave);
        Task DeleteLockModelAsync(LockModel model);

        #endregion

        #region Planner

        Task<List<PlannerGoalDetailsModel>> GetPlannerModelsDataAsync();
        Task SavePlannerModelsDataAsync(List<PlannerGoalDetailsModel> plannerModelsToSave);

        #endregion

        #region Shortcuts

        Task<List<ShortcutGroupModel>> GetShortcutGroupsAsync();
        Task<List<ShortcutModel>> GetDashboardShortcutsAsync();
        Task<ShortcutGroupModel> UpsertShortcutGroupAsync(ShortcutGroupModel group);
        Task<ShortcutModel> SaveShortcutAsync(ShortcutModel shortcut);
        Task DeleteShortcutAsync(long shortcutId);

        #endregion

        #region Reports

        Task<IReadOnlyList<string>> ExecuteSelectForReportAsync(string sql, bool includeHeaderRow = true, params object?[] args);

        Task UpsertReportAsync(ReportModel report);
        Task DeleteReportAsync(int reportId);
        Task<IReadOnlyList<ReportModel>> GetReportsAsync();

        #endregion

        #region Settings

        Task SetStringSettingAsync(string settingKey, string value);
        Task SetBoolSettingAsync(string settingKey, bool value);
        Task SetIntSettingAsync(string settingKey, int value);
        Task SetNullableIntSettingAsync(string settingKey, int? value);
        Task SetDoubleSettingAsync(string settingKey, double value);
        Task<List<AcquiredSetting>> GetSettingsAsync();

        #endregion
    }
    public sealed class HomeSeedData
    {
        public IReadOnlyList<IActiveCardModel> MainQuestCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<IActiveCardModel> MissionCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<ICardModel> BudgetCards { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> Achievements { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> ValueTrackers { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> EventTrackers { get; init; } = new List<ICardModel>();
    }
}
