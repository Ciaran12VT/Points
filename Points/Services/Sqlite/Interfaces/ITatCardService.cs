using Points.Models;

namespace Points.Services.Sqlite.Interfaces
{
    public interface ITatCardService
    {
        Task<TatCardModel> GetTatModelDataAsync(int id);
        Task<List<TatCardModel>> GetTatModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);
        Task SaveTatModelDataAsync(TatCardModel model, long cardId);
    }
}
