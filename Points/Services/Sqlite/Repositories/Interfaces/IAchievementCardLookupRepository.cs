using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IAchievementCardLookupRepository
    {
        Task<AchievementCardModel> GetAchievementCardByIdAsync(int id);
    }
}