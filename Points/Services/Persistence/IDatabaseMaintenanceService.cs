namespace Points.Services.Persistence
{
    public interface IDatabaseMaintenanceService
    {
        Task WipeAsync();
    }
}
