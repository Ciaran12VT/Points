using Points.Evaluators;
using Points.Models;
namespace Points.Services.Sqlite.Interfaces
{
    public interface IAchievementService
    {
        Task MarkAchievementEarnedAsync(long achievementId, DateTime earnedAt);
        Task DeleteAchievementCardModelAsync(AchievementCardModel model);
        Task DeleteAchievementTrophyAsync(int trophyId);

        Task<List<TimeValueAchievementEvaluator>> RefreshEvaluatorsAsync(
            List<TimeValueAchievementEvaluator> timeValueAchievementEvaluators);

        Task<AchievementCardModel> ReevaluateDeadlineAchievementAsync(AchievementCardModel card);
    }



}