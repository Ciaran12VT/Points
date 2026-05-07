namespace Points.Services.Premium;

public sealed class HardcodedPremiumSubscriptionService : IPremiumSubscriptionService
{
    public Task<bool> HasPremiumAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
