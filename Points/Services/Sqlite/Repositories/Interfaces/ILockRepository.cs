using Points.Models;
namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface ILockRepository
    {
        Task<List<LockModel>> GetLocksForCardAsync(long cardId);
        Task SaveLocksForCardAsync(long cardId, List<LockModel> locksToSave);
        Task DeleteLockModelAsync(LockModel model);
    }

}