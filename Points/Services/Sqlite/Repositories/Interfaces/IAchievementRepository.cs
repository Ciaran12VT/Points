using Points.Evaluators;
using Points.Models;
namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IAchievementRepository
    {
        Task DeleteAchievementCardModelAsync(AchievementCardModel model);
        Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt);
        Task DeleteAchievementTrophyAsync(int trophyId);

        Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(
            List<TimeValueAchievementEvaluator> timeValueAchievementEvaluators);

        Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card);
    }

}