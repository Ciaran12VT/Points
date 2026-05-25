#if ANDROID
using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Wearable;
using Points.Services.Time;
using Points.Services.Watch;

namespace Points.Platforms.Android;

public sealed class AndroidWearBridge : IWatchBridge
{
    private readonly Context _context;
    private readonly IClock _clock;

    public AndroidWearBridge(IClock clock)
    {
        _context = global::Android.App.Application.Context;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task PublishSnapshotAsync(string snapshotJson, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var request = PutDataMapRequest.Create(WatchConstants.SnapshotPath);
        request.DataMap.PutString(WatchConstants.SnapshotJsonKey, snapshotJson ?? "");
        request.DataMap.PutLong(WatchConstants.UpdatedAtMillisKey, _clock.UtcNowOffset.ToUnixTimeMilliseconds());

        var putRequest = request.AsPutDataRequest();
        putRequest.SetUrgent();

        await WearableClass.GetDataClient(_context).PutDataItem(putRequest);
    }
}
#endif
