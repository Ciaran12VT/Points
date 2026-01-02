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


        // -----------------------
        // Reads
        // -----------------------

        Task<HomeSeedData> GetHomeSeedDataAsync();

        public Task<List<ValueRateModel>> GetValueRateModelsDataAsync(string whereClause = null);

        public Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(string whereClause = null);

        public Task<List<TatCardModel>> GetTatModelsDataAsync(string whereClause = null);

        public Task<List<ScCardModel>> GetScModelsDataAsync(string whereClause = null);

        public Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string whereClause = null);

        public Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string whereClause = null);

        public Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync(string whereClause = null);

        public Task<ValueRateModel> GetValueRateModelDataAsync(int id);

        public Task<TatCardModel> GetTatModelDataAsync(int id);

        public Task<ScCardModel> GetScModelDataAsync(int id);

        public Task<MissionCardModel> GetMissionCardModelDataAsync(int id);

        public Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id);

        public Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id);


        // -----------------------
        // Writes
        // -----------------------

        Task SaveCardModelsAsync(List<ICardModel> models);

        Task SaveValueRateModelsDataAsync(List<ValueRateModel> models);

        Task SaveTatModelsDataAsync(List<TatCardModel> models);

        Task SaveScModelsDataAsync(List<ScCardModel> models);

        Task SaveMissionCardModelsDataAsync(List<MissionCardModel> models);

        Task SaveBudgetCardModelsDataAsync(List<BudgetCardModel> models);

        Task SaveAchievementCardModelsDataAsync(List<AchievementCardModel> models);


        Task SaveCardModelAsync(ICardModel model);

        Task SaveValueRateModelDataAsync(ValueRateModel model);

        Task SaveTatModelDataAsync(TatCardModel model);

        Task SaveScModelDataAsync(ScCardModel model);

        Task SaveMissionCardModelDataAsync(MissionCardModel model);

        Task SaveBudgetCardModelDataAsync(BudgetCardModel model);

        Task SaveAchievementCardModelDataAsync(AchievementCardModel model);


    }

    public sealed class HomeSeedData
    {
        public IReadOnlyList<IActiveCardModel> MainQuestCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<IActiveCardModel> MissionCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<ICardModel> BudgetCards { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> Achievements { get; init; } = new List<ICardModel>();
    }
}
