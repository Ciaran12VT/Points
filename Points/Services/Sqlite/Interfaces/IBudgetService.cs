using Points.Models;

namespace Points.Services.Sqlite.Interfaces
{
    public interface IBudgetService
    {
        Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id);
        Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string? whereClause = null);
        Task SaveBudgetCardModelDataAsync(BudgetCardModel model, long cardId);
    }
}
