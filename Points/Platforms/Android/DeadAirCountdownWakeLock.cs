#if ANDROID
using Android.Content;
using Android.OS;

namespace Points.Platforms.Android;

internal sealed class DeadAirCountdownWakeLock : IDisposable
{
    private const long SafetyMarginMilliseconds = 2_000;
    private const long MaximumHoldMilliseconds = 62_000;

    private readonly PowerManager.WakeLock? _wakeLock;

    public DeadAirCountdownWakeLock(Context context)
    {
        var powerManager = (PowerManager?)context.GetSystemService(Context.PowerService);
        _wakeLock = powerManager?.NewWakeLock(
            WakeLockFlags.Partial,
            $"{context.PackageName}:dead-air-countdown");
        _wakeLock?.SetReferenceCounted(false);
    }

    public void AcquireUntilContinuousThreshold(TimeSpan elapsed)
    {
        var remaining = TimeSpan.FromSeconds(60) - elapsed;
        if (remaining <= TimeSpan.Zero || _wakeLock == null || _wakeLock.IsHeld)
            return;

        var timeoutMilliseconds = Math.Clamp(
            (long)Math.Ceiling(remaining.TotalMilliseconds) + SafetyMarginMilliseconds,
            1_000,
            MaximumHoldMilliseconds);

        _wakeLock.Acquire(timeoutMilliseconds);
    }

    public void Release()
    {
        if (_wakeLock?.IsHeld == true)
            _wakeLock.Release();
    }

    public void Dispose()
    {
        Release();
        _wakeLock?.Dispose();
    }
}
#endif
