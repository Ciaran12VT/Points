using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IMissionReadRepository
    {
        Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null);
    }
}