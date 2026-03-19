using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface ITrackerReadRepository
    {
        Task<List<ValueTrackerCardModel>> GetValueTrackerCardModelsDataAsync();
        Task<List<EventTrackerCardModel>> GetEventTrackerCardModelsDataAsync();
    }
}