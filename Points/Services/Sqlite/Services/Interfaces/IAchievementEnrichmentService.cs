using Points.Models;

namespace Points.Services.Sqlite.Services.Interfaces
{
    public interface IAchievementEnrichmentService
    {
        Task PopulateAchievementsAsync(
            List<AchievementCardModel> achievements,
            List<IActiveCardModel> mainQuestCards,
            List<MissionCardModel> missionCards);
    }
}