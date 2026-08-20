namespace Points.Services.Notifications;

public sealed class NullActiveCardNotificationAvailabilityService
    : IActiveCardNotificationAvailabilityService
{
    public Task<ActiveCardNotificationAvailability> GetAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ActiveCardNotificationAvailability.Unsupported);
    }

    public Task OpenNotificationSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
