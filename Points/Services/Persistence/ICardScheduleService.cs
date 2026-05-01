using Points.Models;

namespace Points.Services.Persistence
{
    public interface ICardScheduleService
    {
        Task<List<CardSchedule>> GetCardSchedulesForCardAsync(long cardId);
        Task<List<CardSchedule>> GetEnabledCardSchedulesAsync();
        Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId);
        Task SaveCardSchedulesAsync(long cardId, IEnumerable<CardSchedule> schedules);
    }
}
