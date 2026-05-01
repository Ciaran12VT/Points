using Points.Models;

namespace Points.Services.Persistence
{
    public interface IMissionCardService
    {
        Task<MissionCardModel> GetMissionCardModelDataAsync(int id);
        Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null);
        Task SaveMissionCardModelDataAsync(MissionCardModel model, long cardId);
    }
}
