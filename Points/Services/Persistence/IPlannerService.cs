using Points.Models;

namespace Points.Services.Persistence
{
    public interface IPlannerService
    {
        Task<PlannerDayData> GetPlannerDayDataAsync(DateTime plannerDate);
        Task SavePlannerAsync(PlannerModel planner);
    }
}
