namespace Points.Services.Persistence
{
    public interface IHardModePenaltyService
    {
        Task ReconcileAsync(DateTime utcNow);

        Task ReconcileAsync(
            bool hardModeEnabled,
            double penaltyPerMinute,
            bool hasActiveActivity,
            DateTime utcNow);

        Task<double> GetValueAsync(DateTime rangeStart, DateTime rangeEnd, DateTime utcNow);
    }
}
