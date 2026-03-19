using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IScReadRepository
    {
        Task<List<ScCardModel>> GetScModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);
    }
}