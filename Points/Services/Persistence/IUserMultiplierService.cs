using Points.Models;

namespace Points.Services.Persistence
{
    public interface IUserMultiplierService
    {
        Task<List<UserMultiplierModel>> GetMultipliersAsync();

        Task<UserMultiplierModel?> GetActiveMultiplierAsync();

        Task<UserMultiplierModel> SaveMultiplierAsync(UserMultiplierModel multiplier, DateTime utcNow);

        Task DeleteMultiplierAsync(int multiplierId, DateTime utcNow);

        Task SetActiveMultiplierAsync(int? multiplierId, DateTime utcNow);
    }
}
