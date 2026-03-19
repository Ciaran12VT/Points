using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IMissionCardWriteRepository
    {
        Task SaveAsync(MissionCardModel model, long cardId);
    }
}