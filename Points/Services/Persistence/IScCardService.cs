using Points.Models;

namespace Points.Services.Persistence
{
    public interface IScCardService
    {
        Task<ScCardModel> GetScModelDataAsync(int id);
        Task<List<ScCardModel>> GetScModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);
        Task SaveScModelDataAsync(ScCardModel model, long cardId);
        Task RemoveRepForStepAsync(int scCardStepId, DateTime repTime);
    }
}
