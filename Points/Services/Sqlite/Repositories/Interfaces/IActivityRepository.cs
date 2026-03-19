using Points.Models;
namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IActivityRepository
    {
        Task<ActivityModel?> GetCurrentActiveActivityAsync();

        Task<ToggleActivityModelResult> ToggleActivityAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute);

        Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue);

        Task<bool> HasActivityOverlapAsync(
            int excludeActivityId,
            DateTime candidateStart,
            DateTime? candidateEnd);

        Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId);
        Task<DateTime?> GetLastClosedActivityEndUtcAsync();
    }

}