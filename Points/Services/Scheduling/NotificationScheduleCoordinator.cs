using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Scheduling
{
    public sealed class NotificationScheduleCoordinator : INotificationScheduleCoordinator
    {
        private static readonly TimeSpan MissedGracePeriod = TimeSpan.FromMinutes(15);

        private readonly ICardReadService _cards;
        private readonly ICardScheduleService _cardSchedules;
        private readonly INotificationLogService _notificationLogs;
        private readonly IDeviceAlarmScheduler _deviceAlarmScheduler;
        private readonly IScheduleNotificationPresenter _notificationPresenter;
        private readonly IClock _clock;

        public NotificationScheduleCoordinator(
            ICardReadService cards,
            ICardScheduleService cardSchedules,
            INotificationLogService notificationLogs,
            IDeviceAlarmScheduler deviceAlarmScheduler,
            IScheduleNotificationPresenter notificationPresenter,
            IClock clock)
        {
            _cards = cards;
            _cardSchedules = cardSchedules;
            _notificationLogs = notificationLogs;
            _deviceAlarmScheduler = deviceAlarmScheduler;
            _notificationPresenter = notificationPresenter;
            _clock = clock;
        }

        public async Task ScheduleAllAsync(IEnumerable<CardSchedule> schedules, CancellationToken ct = default)
        {
            foreach (var schedule in schedules)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await ScheduleOneAsync(schedule, ct);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to schedule notification for ScheduleId {schedule.ScheduleId}: {ex}");
                }
            }
        }

        public Task ScheduleOneAsync(CardSchedule schedule, CancellationToken ct = default)
        {
            return ScheduleOneAsync(schedule, _clock.LocalNow, ct);
        }

        public Task CancelOneAsync(long scheduleId)
        {
            return _deviceAlarmScheduler.CancelAsync(scheduleId);
        }

        public async Task CancelAllAsync(IEnumerable<long> scheduleIds)
        {
            foreach (var scheduleId in scheduleIds)
                await CancelOneAsync(scheduleId);
        }

        public async Task SyncEnabledSchedulesAsync(CancellationToken ct = default)
        {
            await _notificationLogs.MarkOverdueNotificationLogsMissedAsync(_clock.UtcNow, MissedGracePeriod);

            var schedules = await _cardSchedules.GetEnabledCardSchedulesAsync();
            await ScheduleAllAsync(schedules, ct);
        }

        public async Task HandleScheduleFiredAsync(long scheduleId, DateTime firedAt, CancellationToken ct = default)
        {
            var schedule = await _cardSchedules.GetCardScheduleByIdAsync(scheduleId);
            if (schedule == null)
            {
                await CancelOneAsync(scheduleId);
                return;
            }

            if (!schedule.IsEnabled)
            {
                await CancelOneAsync(scheduleId);
                return;
            }

            var firedAtLocal = WallClockScheduleTime.NormalizeLocal(firedAt);
            var from = WallClockScheduleTime.NormalizeLocal(schedule.FromDateTime);
            var to = WallClockScheduleTime.NormalizeLocal(schedule.ToDateTime);

            if (firedAtLocal < from)
            {
                await ScheduleOneAsync(schedule, firedAtLocal, ct);
                return;
            }

            if (to.HasValue && firedAtLocal > to.Value)
            {
                await CancelOneAsync(schedule.ScheduleId);
                return;
            }

            var title = await _cards.GetCardTitleByIdAsync(schedule.CardId);
            await _notificationPresenter.ShowScheduleFiredAsync(schedule, title, ct);
            await _notificationLogs.MarkNotificationLogSentAsync(schedule, title, firedAtLocal, _clock.UtcNow);

            await ScheduleOneAsync(schedule, firedAtLocal, ct);
        }

        private async Task ScheduleOneAsync(CardSchedule schedule, DateTime now, CancellationToken ct)
        {
            now = WallClockScheduleTime.NormalizeLocal(now);

            if (schedule.ScheduleId <= 0)
                return;

            if (!schedule.IsEnabled)
            {
                await CancelOneAsync(schedule.ScheduleId);
                return;
            }

            var next = CardScheduleOccurrenceCalculator.GetNextOccurrence(schedule, now);
            if (next == null)
            {
                await CancelOneAsync(schedule.ScheduleId);
                return;
            }

            var title = await _cards.GetCardTitleByIdAsync(schedule.CardId);
            var createdAt = _clock.UtcNow;
            var log = await _notificationLogs.UpsertNotificationLogCreatedAsync(schedule, title, next.Value, createdAt);

            try
            {
                await _deviceAlarmScheduler.ScheduleExactAsync(schedule.ScheduleId, next.Value, ct);
                await _notificationLogs.MarkNotificationLogScheduledAsync(log.NotificationLogId, _clock.UtcNow);
            }
            catch (Exception ex)
            {
                await _notificationLogs.MarkNotificationLogScheduleErrorAsync(log.NotificationLogId, ex.Message, _clock.UtcNow);
                throw;
            }
        }
    }
}
