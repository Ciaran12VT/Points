using Points.Models;
namespace Points.Services.Sqlite.Services.Interfaces
{
    public interface IPlannerService
    {
        Task<List<PlannerGoalDetailsModel>> GetPlannerModelsDataAsync();
        Task SavePlannerModelsDataAsync(List<PlannerGoalDetailsModel> plannerModelsToSave);
    }



}