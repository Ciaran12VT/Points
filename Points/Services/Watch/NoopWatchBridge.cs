namespace Points.Services.Watch;

public sealed class NoopWatchBridge : IWatchBridge
{
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishSnapshotAsync(string snapshotJson, CancellationToken ct = default) => Task.CompletedTask;
}
