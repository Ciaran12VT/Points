using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface ITatCardWriteRepository
    {
        Task SaveAsync(TatCardModel model, long cardId);
    }
}