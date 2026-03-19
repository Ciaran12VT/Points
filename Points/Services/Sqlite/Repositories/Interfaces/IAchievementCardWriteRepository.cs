using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IAchievementCardWriteRepository
    {
        Task SaveAsync(AchievementCardModel model, long cardId);
    }
}