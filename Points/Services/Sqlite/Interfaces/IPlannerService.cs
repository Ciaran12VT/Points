using Points.Models;

namespace Points.Services.Sqlite.Interfaces
{
    public interface IPlannerService
    {
        Task<PlannerDayData> GetPlannerDayDataAsync(DateTime plannerDate);
        Task SavePlannerAsync(PlannerModel planner);
    }
}
