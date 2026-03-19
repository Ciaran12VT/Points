using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface ITatReadRepository
    {
        Task<List<TatCardModel>> GetTatModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);
    }
}