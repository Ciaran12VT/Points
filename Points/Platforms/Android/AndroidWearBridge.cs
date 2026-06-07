#if ANDROID
using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Wearable;
using Android.Util;
using Points.Services.Time;
using Points.Services.Watch;

namespace Points.Platforms.Android;

public sealed class AndroidWearBridge : IWatchBridge
{
    private const string Tag = "PointsWearBridge";

    private readonly Context _context;
    private readonly IClock _clock;
    private bool _isWearableApiAvailable = true;

    public AndroidWearBridge(IClock clock)
    {
        _context = global::Android.App.Application.Context;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task PublishSnapshotAsync(string snapshotJson, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_isWearableApiAvailable)
            return;

        var request = PutDataMapRequest.Create(WatchConstants.SnapshotPath);
        request.DataMap.PutString(WatchConstants.SnapshotJsonKey, snapshotJson ?? "");
        request.DataMap.PutLong(WatchConstants.UpdatedAtMillisKey, _clock.UtcNowOffset.ToUnixTimeMilliseconds());

        var putRequest = request.AsPutDataRequest();
        putRequest.SetUrgent();

        try
        {
            await WearableClass.GetDataClient(_context).PutDataItem(putRequest);
        }
        catch (Exception ex) when (IsWearableApiUnavailable(ex))
        {
            _isWearableApiAvailable = false;
            Log.Info(Tag, "Wearable API is not available on this Android device; watch sync is disabled.");
        }
    }

    private static bool IsWearableApiUnavailable(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("Wearable.API", StringComparison.OrdinalIgnoreCase)
            || message.Contains("API_UNAVAILABLE", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
