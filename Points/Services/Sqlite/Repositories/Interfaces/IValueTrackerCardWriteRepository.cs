using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IValueTrackerCardWriteRepository
    {
        Task SaveAsync(ValueTrackerCardModel model, long cardId);
    }
}