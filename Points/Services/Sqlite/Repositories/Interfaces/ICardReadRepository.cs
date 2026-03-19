using Points.Models;
namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface ICardReadRepository
    {
        Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync();
        Task<List<TrophyModel>> GetTrophyModelsDataAsync();
        Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);
        Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId);
        Task<string?> GetCardTitleByIdAsync(long cardId);
    }

}