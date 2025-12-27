#if ANDROID

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Media;
using Android.OS;
using Points.Interfaces;


public sealed class AndroidAudioFeedback : IAudioFeedback, IDisposable
{
    readonly SoundPool _soundPool;
    readonly int _tickId;
    readonly int _clackId;
    readonly int _thockId;

    // Simple rate-limit so you don’t get machine-gun clicks while flinging
    long _lastTickMs;
    const int TickMinIntervalMs = 35;

    public AndroidAudioFeedback()
    {
        _soundPool = Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop
            ? new SoundPool.Builder().SetMaxStreams(4).Build()
            : new SoundPool(4, Android.Media.Stream.Music, 0);

        _tickId = _soundPool.Load(Android.App.Application.Context, global::Points.Resource.Raw.tick, 1);
        _clackId = _soundPool.Load(Android.App.Application.Context, global::Points.Resource.Raw.clack, 1);
        _thockId = _soundPool.Load(Android.App.Application.Context, global::Points.Resource.Raw.thock, 1);
    }

    public void Tick()
    {
        var now = SystemClock.UptimeMillis();
        if (now - _lastTickMs < TickMinIntervalMs) return;
        _lastTickMs = now;

        // leftVol, rightVol, priority, loop, rate
        _soundPool.Play(_tickId, 0.05f, 0.05f, 0, 0, 1.0f);
    }

    public void Thock()
    {
        _soundPool.Play(_thockId, 0.40f, 0.40f, 1, 0, 1.0f);
    }

    public void Dispose()
    {
        _soundPool.Release();
        _soundPool.Dispose();
    }

    public void Clack()
    {
        _soundPool.Play(_clackId, 0.45f, 0.45f, 1, 0, 1.0f);
    }
}


#endif
