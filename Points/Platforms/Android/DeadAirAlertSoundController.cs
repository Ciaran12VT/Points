#if ANDROID
using Android.Content;
using Android.Media;
using Android.OS;

namespace Points.Platforms.Android;

internal enum DeadAirAlertCue
{
    Short,
    Long
}

/// <summary>
/// Owns the Android audio objects used by the Dead Air alert. All state changes
/// are serialized onto the service's main-looper handler and guarded by a
/// service generation so delayed load/focus callbacks cannot revive stale audio.
/// </summary>
internal sealed class DeadAirAlertSoundController : IDisposable
{
    private const long ShortCueCompletionDelayMilliseconds = 350;
    private const long LongCueCompletionDelayMilliseconds = 850;
    private const long PermanentFocusRetryDelayMilliseconds = 5_000;

    private readonly Context _context;
    private readonly Handler _mainHandler;
    private readonly AudioManager _audioManager;
    private readonly AudioAttributes _audioAttributes;
    private readonly SoundPool _soundPool;
    private readonly Action<int> _oneShotCompleted;
    private readonly Action<int, bool> _continuousPlaybackChanged;
    private readonly HashSet<int> _loadedSamples = new();

    private readonly int _shortCueSampleId;
    private readonly int _longCueSampleId;
    private readonly int _loopSampleId;

    private AudioFocusRequestClass? _platformFocusRequest;
    private AudioFocusListener? _focusListener;
    private AudioFocusPurpose _focusPurpose;
    private int _focusEpoch;
    private int _generation;
    private int _pendingOneShotSampleId;
    private DeadAirAlertCue _pendingOneShotCue;
    private int _oneShotStreamId;
    private int _loopStreamId;
    private bool _loopRequested;
    private bool _loopPausedForFocus;
    private bool _focusHeld;
    private bool _focusRequestPending;
    private long _nextPermanentFocusRetryUptime;
    private bool _disposed;

    public DeadAirAlertSoundController(
        Context context,
        Handler mainHandler,
        Action<int> oneShotCompleted,
        Action<int, bool> continuousPlaybackChanged)
    {
        _context = context.ApplicationContext ?? context;
        _mainHandler = mainHandler;
        _oneShotCompleted = oneShotCompleted;
        _continuousPlaybackChanged = continuousPlaybackChanged;

        _audioManager = (AudioManager?)_context.GetSystemService(Context.AudioService)
            ?? throw new InvalidOperationException("Android audio manager is unavailable.");

        _audioAttributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Alarm)
            .SetContentType(AudioContentType.Sonification)
            .Build();

        _soundPool = new SoundPool.Builder()
            .SetMaxStreams(1)
            .SetAudioAttributes(_audioAttributes)
            .Build();

        _soundPool.LoadComplete += OnLoadComplete;
        _shortCueSampleId = _soundPool.Load(_context, Resource.Raw.dead_air_alert_short, 1);
        _longCueSampleId = _soundPool.Load(_context, Resource.Raw.dead_air_alert_long, 1);
        _loopSampleId = _soundPool.Load(_context, Resource.Raw.dead_air_alert_cycle, 1);
    }

    public bool TryPlayOneShot(DeadAirAlertCue cue, int generation)
    {
        EnsureMainThread();
        ThrowIfDisposed();
        BeginGeneration(generation);

        if (_loopRequested)
            return false;

        var sampleId = cue == DeadAirAlertCue.Short
            ? _shortCueSampleId
            : _longCueSampleId;

        StopOneShot(notifyCompletion: false);
        _pendingOneShotSampleId = sampleId;
        _pendingOneShotCue = cue;

        if (!_loadedSamples.Contains(sampleId))
            return true;

        return TryStartPendingOneShot(generation);
    }

    private bool TryStartPendingOneShot(int generation)
    {
        if (_disposed
            || generation != _generation
            || _pendingOneShotSampleId == 0
            || !_loadedSamples.Contains(_pendingOneShotSampleId))
        {
            return false;
        }

        var sampleId = _pendingOneShotSampleId;
        var cue = _pendingOneShotCue;
        _pendingOneShotSampleId = 0;

        var focusResult = RequestAudioFocus(AudioFocusPurpose.OneShot, acceptDelayed: false);
        if (focusResult != AudioFocusRequest.Granted)
        {
            AbandonAudioFocus();
            return false;
        }

        _oneShotStreamId = _soundPool.Play(sampleId, 1f, 1f, priority: 1, loop: 0, rate: 1f);
        if (_oneShotStreamId == 0)
        {
            AbandonAudioFocus();
            return false;
        }

        var streamId = _oneShotStreamId;
        var completionDelay = cue == DeadAirAlertCue.Short
            ? ShortCueCompletionDelayMilliseconds
            : LongCueCompletionDelayMilliseconds;

        _mainHandler.PostDelayed(
            () => CompleteOneShot(generation, streamId),
            completionDelay);

        return true;
    }

    public void EnsureLoop(int generation)
    {
        EnsureMainThread();
        ThrowIfDisposed();
        BeginGeneration(generation);

        _loopRequested = true;
        StopOneShot(notifyCompletion: false);
        TryStartOrResumeLoop();
    }

    public void StopAll(int generation)
    {
        EnsureMainThread();

        _generation = generation;
        _loopRequested = false;
        _loopPausedForFocus = false;
        _focusRequestPending = false;
        _nextPermanentFocusRetryUptime = 0;
        _pendingOneShotSampleId = 0;

        StopOneShot(notifyCompletion: false);
        StopLoop();
        AbandonAudioFocus();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        EnsureMainThread();
        StopAll(unchecked(_generation + 1));
        _soundPool.LoadComplete -= OnLoadComplete;
        _soundPool.Release();
        _soundPool.Dispose();
        _audioAttributes.Dispose();
        _disposed = true;
    }

    private void BeginGeneration(int generation)
    {
        if (_generation == generation)
            return;

        StopAll(generation);
    }

    private void OnLoadComplete(object? sender, SoundPool.LoadCompleteEventArgs args)
    {
        var sampleId = args.SampleId;
        var status = args.Status;
        _mainHandler.Post(() =>
        {
            if (_disposed)
                return;

            if (status != 0)
            {
                if (sampleId == _pendingOneShotSampleId)
                {
                    _pendingOneShotSampleId = 0;
                    _oneShotCompleted(_generation);
                }

                return;
            }

            _loadedSamples.Add(sampleId);
            if (sampleId == _pendingOneShotSampleId
                && !TryStartPendingOneShot(_generation))
            {
                _oneShotCompleted(_generation);
            }

            if (_loopRequested && sampleId == _loopSampleId)
                TryStartOrResumeLoop();
        });
    }

    private void TryStartOrResumeLoop()
    {
        if (_disposed || !_loopRequested || !_loadedSamples.Contains(_loopSampleId))
            return;

        if (_loopStreamId != 0 && _loopPausedForFocus && _focusHeld)
        {
            _soundPool.Resume(_loopStreamId);
            _loopPausedForFocus = false;
            _continuousPlaybackChanged(_generation, true);
            return;
        }

        if (_loopStreamId != 0 || _focusRequestPending)
            return;

        if (SystemClock.UptimeMillis() < _nextPermanentFocusRetryUptime)
            return;

        var focusResult = RequestAudioFocus(AudioFocusPurpose.Continuous, acceptDelayed: true);
        if (focusResult == AudioFocusRequest.Delayed)
        {
            _focusRequestPending = true;
            return;
        }

        if (focusResult != AudioFocusRequest.Granted)
        {
            _nextPermanentFocusRetryUptime =
                SystemClock.UptimeMillis() + PermanentFocusRetryDelayMilliseconds;
            AbandonAudioFocus();
            return;
        }

        StartLoop();
    }

    private void StartLoop()
    {
        if (!_loopRequested || !_focusHeld || _loopStreamId != 0)
            return;

        _loopStreamId = _soundPool.Play(
            _loopSampleId,
            1f,
            1f,
            priority: 1,
            loop: -1,
            rate: 1f);

        if (_loopStreamId == 0)
        {
            _nextPermanentFocusRetryUptime =
                SystemClock.UptimeMillis() + PermanentFocusRetryDelayMilliseconds;
            AbandonAudioFocus();
            return;
        }

        _continuousPlaybackChanged(_generation, true);
    }

    private AudioFocusRequest RequestAudioFocus(
        AudioFocusPurpose purpose,
        bool acceptDelayed)
    {
        if (_focusHeld && _focusPurpose == purpose)
            return AudioFocusRequest.Granted;

        AbandonAudioFocus();
        _focusPurpose = purpose;
        var focusEpoch = AdvanceFocusEpoch();
        var focusListener = new AudioFocusListener(this, focusEpoch);
        _focusListener = focusListener;

        try
        {
            AudioFocusRequest result;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var focusGain = purpose == AudioFocusPurpose.Continuous
                    ? AudioFocus.Gain
                    : AudioFocus.GainTransient;
                _platformFocusRequest = new AudioFocusRequestClass.Builder(focusGain)
                    .SetAudioAttributes(_audioAttributes)
                    .SetWillPauseWhenDucked(true)
                    .SetAcceptsDelayedFocusGain(acceptDelayed)
                    .SetOnAudioFocusChangeListener(focusListener, _mainHandler)
                    .Build();

                result = _audioManager.RequestAudioFocus(_platformFocusRequest);
            }
            else
            {
#pragma warning disable CA1422
                result = _audioManager.RequestAudioFocus(
                    focusListener,
                    global::Android.Media.Stream.Alarm,
                    purpose == AudioFocusPurpose.Continuous
                        ? AudioFocus.Gain
                        : AudioFocus.GainTransient);
#pragma warning restore CA1422
            }

            _focusHeld = result == AudioFocusRequest.Granted;
            _focusRequestPending = result == AudioFocusRequest.Delayed;
            return result;
        }
        catch
        {
            AbandonAudioFocus();
            throw;
        }
    }

    private void HandleAudioFocusChange(AudioFocus focusChange, int focusEpoch)
    {
        if (focusEpoch != Volatile.Read(ref _focusEpoch))
            return;

        if (Looper.MyLooper() == Looper.MainLooper)
        {
            ApplyAudioFocusChange(focusChange, focusEpoch);
            return;
        }

        _mainHandler.Post(() => ApplyAudioFocusChange(focusChange, focusEpoch));
    }

    private void ApplyAudioFocusChange(AudioFocus focusChange, int focusEpoch)
    {
        EnsureMainThread();

        if (_disposed || focusEpoch != Volatile.Read(ref _focusEpoch))
            return;

        switch (focusChange)
        {
            case AudioFocus.Gain:
                _focusHeld = true;
                _focusRequestPending = false;
                _nextPermanentFocusRetryUptime = 0;
                if (_focusPurpose == AudioFocusPurpose.Continuous)
                    TryStartOrResumeLoop();
                break;

            case AudioFocus.LossTransient:
            case AudioFocus.LossTransientCanDuck:
                _focusHeld = false;
                _focusRequestPending = true;
                if (_loopStreamId != 0 && !_loopPausedForFocus)
                {
                    _soundPool.Pause(_loopStreamId);
                    _loopPausedForFocus = true;
                    _continuousPlaybackChanged(_generation, false);
                }
                else if (_oneShotStreamId != 0)
                {
                    StopOneShot(notifyCompletion: true);
                }
                break;

            case AudioFocus.Loss:
                _focusHeld = false;
                _focusRequestPending = false;
                _nextPermanentFocusRetryUptime =
                    SystemClock.UptimeMillis() + PermanentFocusRetryDelayMilliseconds;
                StopOneShot(notifyCompletion: true);
                StopLoop();
                break;
        }
    }

    private void CompleteOneShot(int generation, int streamId)
    {
        if (_disposed || generation != _generation || streamId != _oneShotStreamId)
            return;

        StopOneShot(notifyCompletion: true);
    }

    private void StopOneShot(bool notifyCompletion)
    {
        _pendingOneShotSampleId = 0;

        if (_oneShotStreamId == 0)
            return;

        _soundPool.Stop(_oneShotStreamId);
        _oneShotStreamId = 0;

        if (_focusPurpose == AudioFocusPurpose.OneShot)
            AbandonAudioFocus();

        if (notifyCompletion)
            _oneShotCompleted(_generation);
    }

    private void StopLoop()
    {
        if (_loopStreamId == 0)
            return;

        var wasPlaying = !_loopPausedForFocus;
        _soundPool.Stop(_loopStreamId);
        _loopStreamId = 0;
        _loopPausedForFocus = false;

        if (wasPlaying)
            _continuousPlaybackChanged(_generation, false);
    }

    private void AbandonAudioFocus()
    {
        var platformFocusRequest = _platformFocusRequest;
        var focusListener = _focusListener;
        var hadFocusPurpose = _focusPurpose != AudioFocusPurpose.None;

        _platformFocusRequest = null;
        _focusListener = null;
        AdvanceFocusEpoch();

        try
        {
            if (platformFocusRequest != null && Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                _audioManager.AbandonAudioFocusRequest(platformFocusRequest);
            }
            else if (hadFocusPurpose && focusListener != null)
            {
#pragma warning disable CA1422
                _audioManager.AbandonAudioFocus(focusListener);
#pragma warning restore CA1422
            }
        }
        finally
        {
            platformFocusRequest?.Dispose();
            focusListener?.Dispose();

            _focusHeld = false;
            _focusRequestPending = false;
            _focusPurpose = AudioFocusPurpose.None;
        }
    }

    private int AdvanceFocusEpoch()
    {
        return Interlocked.Increment(ref _focusEpoch);
    }

    private static void EnsureMainThread()
    {
        if (Looper.MyLooper() != Looper.MainLooper)
            throw new InvalidOperationException("Dead Air audio state must run on Android's main looper.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private enum AudioFocusPurpose
    {
        None,
        OneShot,
        Continuous
    }

    private sealed class AudioFocusListener : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        private readonly WeakReference<DeadAirAlertSoundController> _owner;
        private readonly int _focusEpoch;

        public AudioFocusListener(DeadAirAlertSoundController owner, int focusEpoch)
        {
            _owner = new WeakReference<DeadAirAlertSoundController>(owner);
            _focusEpoch = focusEpoch;
        }

        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            if (_owner.TryGetTarget(out var owner))
                owner.HandleAudioFocusChange(focusChange, _focusEpoch);
        }
    }
}
#endif
