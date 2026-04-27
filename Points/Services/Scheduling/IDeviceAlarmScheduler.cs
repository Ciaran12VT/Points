namespace Points.Services.Scheduling
{
    public interface IDeviceAlarmScheduler
    {
        Task ScheduleExactAsync(long scheduleId, DateTime scheduleFor, CancellationToken ct = default);
        Task CancelAsync(long scheduleId);
    }
}
