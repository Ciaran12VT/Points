using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IScCardWriteRepository
    {
        Task SaveAsync(ScCardModel model, long cardId);
    }
}