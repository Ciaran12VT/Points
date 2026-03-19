using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IBudgetReadRepository
    {
        Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync();
    }
}