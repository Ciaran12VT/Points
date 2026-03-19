using Points.Models;
namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IPlannerRepository
    {
        Task<List<PlannerGoalDetailsModel>> GetPlannerModelsDataAsync();
        Task SavePlannerModelsDataAsync(List<PlannerGoalDetailsModel> plannerModelsToSave);
    }

}