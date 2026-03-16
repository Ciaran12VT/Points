using Points.Models;
namespace Points.Services.Sqlite.Interfaces
{
    public interface IActivityService
    {
        Task<ActivityModel?> GetCurrentActiveActivityAsync();

        Task<ToggleActivityModelResult> ToggleActivityAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute);

        Task<bool> HasActivityOverlapAsync(
            int excludeActivityId,
            DateTime candidateStart,
            DateTime? candidateEnd);

        Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId);
        Task<DateTime?> GetLastClosedActivityEndUtcAsync();

        Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue);
    }



}