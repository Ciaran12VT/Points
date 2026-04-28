using Points.Models;
namespace Points.Services.Sqlite.Interfaces
{
    public interface IGoalService
    {
        Task<List<GoalDetailsModel>> GetGoalModelsDataAsync();
        Task SaveGoalModelsDataAsync(List<GoalDetailsModel> goalModelsToSave);
    }



}