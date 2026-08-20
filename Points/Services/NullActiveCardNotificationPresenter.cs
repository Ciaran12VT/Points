namespace Points.Services;

public sealed class NullActiveCardNotificationPresenter : IActiveCardNotificationPresenter
{
    public Task PresentAsync(
        ActiveCardNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
