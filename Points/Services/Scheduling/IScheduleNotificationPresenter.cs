using Points.Models;

namespace Points.Services.Scheduling
{
    public interface IScheduleNotificationPresenter
    {
        Task ShowScheduleFiredAsync(CardSchedule schedule, string? title, CancellationToken ct = default);
    }
}
