using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IEventTrackerCardWriteRepository
    {
        Task SaveAsync(EventTrackerCardModel model, long cardId);
    }
}