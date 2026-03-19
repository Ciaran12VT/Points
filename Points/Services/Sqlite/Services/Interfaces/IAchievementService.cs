using Points.Evaluators;
using Points.Models;
using SQLite;
namespace Points.Services.Sqlite.Services.Interfaces
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