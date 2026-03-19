using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IBudgetCardWriteRepository
    {
        Task SaveAsync(BudgetCardModel model, long cardId);
    }
}