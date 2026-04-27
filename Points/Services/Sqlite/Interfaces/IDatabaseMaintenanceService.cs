namespace Points.Services.Sqlite.Interfaces
{
    public interface IDatabaseMaintenanceService
    {
        Task WipeAsync();
    }
}
