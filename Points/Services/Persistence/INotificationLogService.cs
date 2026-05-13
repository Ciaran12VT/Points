using Points.Models;

namespace Points.Services.Persistence
{
    public interface INotificationLogService
    {
        Task<IReadOnlyList<NotificationLogModel>> GetNotificationLogsAsync(int limit = 250);

        Task<IReadOnlyList<NotificationLogModel>> GetNotificationLogsAsync(
            NotificationLogFilter filter,
            int offset,
            int limit);

        Task<int> GetNotificationLogCountAsync(NotificationLogFilter filter);

        Task<NotificationLogModel> UpsertNotificationLogCreatedAsync(
            CardSchedule schedule,
            string? cardTitle,
            DateTime scheduleFor,
            DateTime createdAt);

        Task MarkNotificationLogScheduledAsync(long notificationLogId, DateTime scheduledAt);

        Task MarkNotificationLogScheduleErrorAsync(long notificationLogId, string error, DateTime updatedAt);

        Task MarkNotificationLogSentAsync(
            CardSchedule schedule,
            string? cardTitle,
            DateTime firedAt,
            DateTime sentAt);

        Task MarkOverdueNotificationLogsMissedAsync(DateTime now, TimeSpan gracePeriod);

        Task MarkNotificationLogsMissedSeenAsync(IEnumerable<long> notificationLogIds, DateTime seenAt);
    }
}
