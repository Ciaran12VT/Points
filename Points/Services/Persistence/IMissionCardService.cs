using Points.Models;

namespace Points.Services.Persistence
{
    public interface IMissionCardService
    {
        Task<MissionCardModel> GetMissionCardModelDataAsync(int id);
        Task<MissionCardModel?> GetMissionCardModelByGuidAsync(Guid missionGuid);
        Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null);
        Task SaveMissionCardModelDataAsync(MissionCardModel model, long cardId);
    }
}
