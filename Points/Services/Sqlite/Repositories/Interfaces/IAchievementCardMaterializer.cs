using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IAchievementCardMaterializer
    {
        Task<AchievementCardModel> MaterializeAsync(CardReadRepository.AchievementCardJoinedRow row);
    }
}