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
        // Reads
        // -----------------------

        Task<HomeSeedData> GetHomeSeedDataAsync();

        public Task<List<ValueRateModel>> GetValueRateModelDataAsync();

        public Task<List<IActiveCardModel>> GetMainQuestModelDataAsync();

        public Task<List<TatCardModel>> GetTatModelDataAsync();

        public Task<List<ScCardModel>> GetScModelDataAsync();

        public Task<List<MissionCardModel>> GetMissionCardModelDataAsync();

        public Task<List<BudgetCardModel>> GetBudgetCardModelDataAsync();

        public Task<List<AchievementCardModel>> GetAchievementCardModelDataAsync();

        // -----------------------
        // Writes
        // -----------------------

        Task SaveCardModelAsync(List<ICardModel> models);
        Task SaveCardModelAsync(ICardModel model);

        Task SaveValueRateModelDataAsync(List<ValueRateModel> models);

        Task SaveTatModelDataAsync(List<TatCardModel> models);

        Task SaveScModelDataAsync(List<ScCardModel> models);

        Task SaveMissionCardModelDataAsync(List<MissionCardModel> models);

        Task SaveBudgetCardModelDataAsync(List<BudgetCardModel> models);

        Task SaveAchievementCardModelDataAsync(List<AchievementCardModel> models);

        Task SaveTatModelAsync(TatCardModel model);

        Task SaveScModelAsync(ScCardModel model);

        Task SaveMissionCardModelAsync(MissionCardModel model);

        Task SaveBudgetCardModelAsync(BudgetCardModel model);

        Task SaveAchievementCardModelAsync(AchievementCardModel model);


    }

    public sealed class HomeSeedData
    {
        public IReadOnlyList<IActiveCardModel> MainQuestCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<IActiveCardModel> MissionCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<ICardModel> BudgetCards { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> Achievements { get; init; } = new List<ICardModel>();
    }
}
