namespace Points.Services;

public interface IActiveCardNotificationPresenter
{
    Task PresentAsync(
        ActiveCardNotificationRequest request,
        CancellationToken cancellationToken = default);
}
