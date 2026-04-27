using Points.Models;

namespace Points.Services.Scheduling
{
    public interface INotificationScheduleCoordinator
    {
        Task ScheduleAllAsync(IEnumerable<CardSchedule> schedules, CancellationToken ct = default);
        Task ScheduleOneAsync(CardSchedule schedule, CancellationToken ct = default);
        Task CancelOneAsync(long scheduleId);
        Task CancelAllAsync(IEnumerable<long> scheduleIds);
        Task SyncEnabledSchedulesAsync(CancellationToken ct = default);
        Task HandleScheduleFiredAsync(long scheduleId, DateTime firedAt, CancellationToken ct = default);
    }
}
