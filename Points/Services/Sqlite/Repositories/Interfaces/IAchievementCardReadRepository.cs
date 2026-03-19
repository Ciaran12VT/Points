using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IAchievementCardReadRepository
    {
        Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync();
    }
}