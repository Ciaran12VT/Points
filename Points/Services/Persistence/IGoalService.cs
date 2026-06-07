using Points.Models;
namespace Points.Services.Persistence
{
    public interface IGoalService
    {
        Task<List<GoalDetailsModel>> GetGoalModelsDataAsync();
        Task SaveGoalModelsDataAsync(List<GoalDetailsModel> goalModelsToSave);
    }



}