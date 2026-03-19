using Points.Models;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IMainQuestReadRepository
    {
        Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);
    }
}