namespace Points.Services.Premium;

public interface IPremiumSubscriptionService
{
    Task<bool> HasPremiumAsync(CancellationToken cancellationToken = default);
}
