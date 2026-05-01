using Points.Models;
namespace Points.Services.Persistence
{
    public interface ILockService
    {
        Task<List<LockModel>> GetLocksForCardAsync(long cardId);
        Task SaveLocksForCardAsync(long cardId, List<LockModel> locksToSave);
        Task DeleteLockModelAsync(LockModel model);
    }



}