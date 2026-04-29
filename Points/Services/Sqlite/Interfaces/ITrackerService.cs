using Points.Models;

namespace Points.Services.Sqlite.Interfaces
{
    public interface ITrackerService
    {
        Task<ValueTrackerCardModel> GetValueTrackerCardModelDataAsync(int id);
        Task<List<ValueTrackerCardModel>> GetValueTrackerCardModelsDataAsync(string? whereClause = null);
        Task<EventTrackerCardModel> GetEventTrackerCardModelDataAsync(int id);
        Task<List<EventTrackerCardModel>> GetEventTrackerCardModelsDataAsync(string? whereClause = null);
        Task SaveValueTrackerCardModelDataAsync(ValueTrackerCardModel model, long cardId);
        Task SaveEventTrackerCardModelDataAsync(EventTrackerCardModel model, long cardId);
    }
}
