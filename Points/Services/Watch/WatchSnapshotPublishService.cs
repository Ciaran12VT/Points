using Points.Services.Diagnostics;
using Points.Services.Time;
using System.Diagnostics;

namespace Points.Services.Watch;

public sealed class WatchSnapshotPublishService : IWatchSnapshotPublishService
{
    private static readonly TimeSpan MinimumPublishInterval = TimeSpan.FromSeconds(1);

    private readonly IWatchSnapshotBuilder _builder;
    private readonly IWatchBridge _bridge;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _publishGate = new(1, 1);

    private DateTime _lastPublishedUtc = DateTime.MinValue;
    private string? _lastPublishedJson;
    private int _pendingPublish;

    public WatchSnapshotPublishService(IWatchSnapshotBuilder builder, IWatchBridge bridge, IClock clock)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task RequestPublishAsync(bool force = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!force && _clock.UtcNow - _lastPublishedUtc < MinimumPublishInterval)
        {
            if (Interlocked.Exchange(ref _pendingPublish, 1) == 0)
                DelayedPublishAsync().Forget("Delayed watch snapshot publish");

            return;
        }

        await PublishNowAsync(force, ct);
    }

    private async Task DelayedPublishAsync()
    {
        var delay = MinimumPublishInterval - (_clock.UtcNow - _lastPublishedUtc);
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay);

        Interlocked.Exchange(ref _pendingPublish, 0);
        await PublishNowAsync(force: false, CancellationToken.None);
    }

    private async Task PublishNowAsync(bool force, CancellationToken ct)
    {
        if (!await _publishGate.WaitAsync(0, ct))
        {
            Interlocked.Exchange(ref _pendingPublish, 1);
            return;
        }

        try
        {
            var json = await _builder.BuildSnapshotJsonAsync(ct);

            if (!force && string.Equals(json, _lastPublishedJson, StringComparison.Ordinal))
                return;

            try
            {
                await _bridge.PublishSnapshotAsync(json, ct);
                _lastPublishedJson = json;
                _lastPublishedUtc = _clock.UtcNow;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Watch snapshot publish failed: {ex}");
            }
        }
        finally
        {
            _publishGate.Release();
        }
    }
}
