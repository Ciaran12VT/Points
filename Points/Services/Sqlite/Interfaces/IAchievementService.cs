using Points.Evaluators;
using Points.Models;
namespace Points.Services.Sqlite.Interfaces
{
    public interface IAchievementService
    {
        Task<AchievementCardModel> GetAchievementCardModelDataAsync(int id);
        Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync();
        Task<List<TrophyModel>> GetTrophyModelsDataAsync();
        Task SaveAchievementCardModelDataAsync(AchievementCardModel acm, long cardId);
        Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt);
        Task DeleteAchievementCardModelAsync(AchievementCardModel model);
        Task DeleteAchievementTrophyAsync(int trophyId);
        Task PopulateAchievementsAsync(
            List<AchievementCardModel> achievements,
            List<IActiveCardModel> mainQuest,
            List<MissionCardModel> mission);

        Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(
            List<TimeValueAchievementEvaluator> timeValueAchievementEvaluators);

        Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card);
    }



}
