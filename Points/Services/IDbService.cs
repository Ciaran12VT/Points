using Points.Models;
using Points.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services
{
    public interface IDbService : IDatabaseMaintenance
    {
        // -----------------------
        // Initialisation
        // -----------------------

        Task InitializeAsync();


        // -------------------------
        // Backups and DB Maintenance
        // -------------------------

        Task BackupAsync();
        Task WipeAsync();
        Task RestoreAsync(string backupFilePath);
        DateTime? GetLastBackupUtc();

        Task CloseDatabaseAsync();

        Task ReinitializeDatabaseAsync();

        // -----------------------
        // Reads
        // -----------------------

        // Achievement
        //Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id);
        //Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync(string whereClause = null);
        Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync(string whereClause = null);

        // Budget
        Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id);
        Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string whereClause = null);

        // Home seed
        Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd);

        // Main Quest (combined)
        Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);

        // Mission
        Task<MissionCardModel> GetMissionCardModelDataAsync(int id);
        Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string whereClause = null);

        // SC
        Task<ScCardModel> GetScModelDataAsync(int id);
        Task<List<ScCardModel>> GetScModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);

        // TAT
        Task<TatCardModel> GetTatModelDataAsync(int id);
        Task<List<TatCardModel>> GetTatModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);


        // Trackers
        Task<ValueTrackerCardModel> GetValueTrackerCardModelDataAsync(int id);
        Task<List<ValueTrackerCardModel>> GetValueTrackerCardModelsDataAsync(string whereClause = null);

        Task<EventTrackerCardModel> GetEventTrackerCardModelDataAsync(int id);
        Task<List<EventTrackerCardModel>> GetEventTrackerCardModelsDataAsync(string whereClause = null);


        // -----------------------
        // Writes
        // -----------------------

        Task SaveCardModelAsync(ICardModel model);
        Task SaveCardModelsAsync(List<ICardModel> models);

        Task SaveAchievementCardModelDataAsync(AchievementCardModel acm, long cardId);
        Task DeleteAchievementCardModelAsync(AchievementCardModel model);

        // Adds a new entity to ScCardStepRep for the step
        Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue);

        // Removes the last rep before/at the given time (your implementation treats param as ScCardStepID)
        Task RemoveRepForStep(int scCardStepID, DateTime repTime);

        // Adds an Activity row for the card resolved via model type + model.Id
        Task<int> AddActivity(IActiveCardModel model, DateTime startTime);

        // Ends the most recent/open Activity row for the card resolved via model type + model.Id
        Task EndActivity(IActiveCardModel model, DateTime endTime);
        Task<Tuple<DateTime,DateTime>> GetPreviousAndNextActivePeriodDateTimes(DateTime current);

        Task<IReadOnlyList<string>> ExecuteSelectForReportAsync(string sql, bool includeHeaderRow = true, params object?[] args);

        Task<int> CloseAnyOpenActivitiesAsync();

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
