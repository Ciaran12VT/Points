namespace Points.Services.Notifications;

public enum ActiveCardNotificationAvailabilityStatus
{
    Unknown,
    Available,
    PermissionDenied,
    AppNotificationsDisabled,
    ChannelDisabled,
    UnsupportedPlatform
}

public sealed record ActiveCardNotificationAvailability(
    ActiveCardNotificationAvailabilityStatus Status)
{
    public bool IsAvailable => Status == ActiveCardNotificationAvailabilityStatus.Available;

    public bool CanOpenSettings => Status is
        ActiveCardNotificationAvailabilityStatus.PermissionDenied or
        ActiveCardNotificationAvailabilityStatus.AppNotificationsDisabled or
        ActiveCardNotificationAvailabilityStatus.ChannelDisabled;

    public static ActiveCardNotificationAvailability Unknown { get; } =
        new(ActiveCardNotificationAvailabilityStatus.Unknown);

    public static ActiveCardNotificationAvailability Available { get; } =
        new(ActiveCardNotificationAvailabilityStatus.Available);

    public static ActiveCardNotificationAvailability Unsupported { get; } =
        new(ActiveCardNotificationAvailabilityStatus.UnsupportedPlatform);
}

public interface IActiveCardNotificationAvailabilityService
{
    Task<ActiveCardNotificationAvailability> GetAvailabilityAsync(
        CancellationToken cancellationToken = default);

    Task OpenNotificationSettingsAsync(
        CancellationToken cancellationToken = default);
}
