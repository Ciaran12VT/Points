using Points.Models;

namespace Points.Services.Planner;

public interface IPlannerCardSource
{
    Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);
    Task<List<MissionCardModel>> GetMissionCardModelsDataAsync(string? whereClause = null);
}
