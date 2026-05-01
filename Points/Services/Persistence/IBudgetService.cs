using Points.Models;

namespace Points.Services.Persistence
{
    public interface IBudgetService
    {
        Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id);
        Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string? whereClause = null);
        Task SaveBudgetCardModelDataAsync(BudgetCardModel model, long cardId);
    }
}
