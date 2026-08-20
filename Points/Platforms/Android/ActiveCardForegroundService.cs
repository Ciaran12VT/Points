#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Android.Graphics;
using Android.Content.PM;
using System.Text.Json;
using Points.Models;
using am = Android.Media;
using System.Threading;
using Points.Helpers;
using Points.Services;
using Points.Services.Diagnostics;
using Points.Services.Persistence;
using Points.Services.Time;

// Namespace: use your actual root namespace
namespace Points.Platforms.Android
{
    [Service(
        Name = "com.companyname.points.ActiveCardForegroundService",
        ForegroundServiceType = ForegroundService.TypeSpecialUse | ForegroundService.TypeMediaPlayback,
        Exported = false)]
    public class ActiveCardForegroundService : Service
    {
        //Intent Extras
        public const string ExtraCardJson = "EXTRA_ACTIVE_CARD_JSON";
        public const string ExtraSessionId = "EXTRA_SERVICE_SESSION_ID";
        public const string ExtraNotificationMode = "EXTRA_ACTIVE_CARD_NOTIFICATION_MODE";
        public const string ExtraDeadAirStartedAtUtc = "EXTRA_DEAD_AIR_STARTED_AT_UTC";
        public const string ExtraDeadAirAlertNoiseRequested = "EXTRA_DEAD_AIR_ALERT_NOISE_REQUESTED";
        public const string ActionOpenActiveCard = "com.companyname.points.OPEN_ACTIVE_CARD";
        public const string ActionOpenHome = "com.companyname.points.OPEN_HOME";
        public const string ExtraOpenActiveCard = "EXTRA_OPEN_ACTIVE_CARD";
        public const string ExtraTargetCardId = "EXTRA_TARGET_CARD_ID";

        //Channel meta-data
        public const string NotificationChannelId = "points_active_card_channel";
        const string NotificationChannelName = "Active card";
        public const int NotificationId = 1001;

        // ?? New: alert channel for noisy/vibrating notifications
        const string AlertChannelId = "points_active_card_alert_channel_v2";
        const string AlertChannelName = "Active card alerts";
        const int AlertNotificationId = 2001;

        // ?? Achievement alert channel (noisy)
        const string AchievementChannelId = "points_achievement_alert_channel_v1";
        const string AchievementChannelName = "Achievements";
        const int AchievementNotificationBaseId = 3000;

        //Active Card
        private volatile ActiveCardNotificationMode _notificationMode = ActiveCardNotificationMode.None;
        private IActiveCardModel? _activeCard;
        private DateTime _activeDayStartLocal;
        private DateTime? _deadAirStartedAtUtc;
        private bool _deadAirAlertNoiseRequested;
        private DeadAirAlertState _deadAirAlertState = DeadAirAlertState.Initial();
        private volatile int _deadAirAlertGeneration;
        private TimeSpan _deadAirAlertElapsedAtAnchor;
        private long _deadAirAlertAnchorElapsedRealtimeMilliseconds;
        private int _deadAirAlertEvaluationPosted;
        private Handler? _mainHandler;
        private DeadAirAlertSoundController? _deadAirAlertSoundController;
        private DeadAirAlertStateStore? _deadAirAlertStateStore;
        private DeadAirCountdownWakeLock? _deadAirCountdownWakeLock;
        private Notification? _currentForegroundNotification;
        private bool _foregroundIncludesMediaPlayback;
        private System.Threading.CancellationTokenSource? _cts;
        private Task? _timerTask;

        //Shared Preferences Meta-data
        const string PrefsName = "points_service_prefs";
        const string KeySessionId = "active_card_service_session_id";
        private string _sessionId = "";

        private bool _timerToTriggerExecuted = false; //Tracks if a timer fo rthis activity has already been executed so as not to execute it again.
        private volatile bool _achievementsSeededForCurrentCard = true;      
        private int _achievementRefreshInProgress = 0; // 0 = not running, 1 = running

        private readonly HashSet<int> _earnedThisSession = new(); //Tracks achievements earned this session

        #region Foreground Service Life-cycle hooks
        public override void OnCreate()
        {
            base.OnCreate();

            var prefs = GetSharedPreferences(PrefsName, FileCreationMode.Private);
            _sessionId = Guid.NewGuid().ToString("N");
            prefs.Edit()!.PutString(KeySessionId, _sessionId).Apply();

            _mainHandler = new Handler(Looper.MainLooper!);
            _deadAirAlertStateStore = new DeadAirAlertStateStore(this, PrefsName);
            _deadAirCountdownWakeLock = new DeadAirCountdownWakeLock(this);
        }

        public override void OnDestroy()
        {
            ClearNotificationState(cancelTimer: true);
            _deadAirAlertSoundController?.Dispose();
            _deadAirAlertSoundController = null;
            _deadAirCountdownWakeLock?.Dispose();
            _deadAirCountdownWakeLock = null;
            _mainHandler?.Dispose();
            _mainHandler = null;
            base.OnDestroy();
        }

        public override IBinder? OnBind(Intent? intent) => null;

        #endregion

        // Foreground service entry point for applying an explicit notification mode.
        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            try
            {
                if (!TryReadMode(intent, out var mode))
                    return StopForInvalidRequest(startId, "Missing or invalid notification mode.");

                var clock = ServiceHelper.GetService<IClock>();
                var applied = mode switch
                {
                    ActiveCardNotificationMode.ActiveCard => TryApplyActiveCard(intent!, clock),
                    ActiveCardNotificationMode.DeadAir => TryApplyDeadAir(
                        intent!,
                        (flags & StartCommandFlags.Redelivery) != 0,
                        clock),
                    _ => false
                };

                if (!applied)
                    return StopForInvalidRequest(startId, $"Invalid payload for notification mode '{mode}'.");

                CreateNotificationChannel();
                _currentForegroundNotification = BuildCurrentNotification(clock);
                StartOrUpdateForeground(_foregroundIncludesMediaPlayback);

                if (_notificationMode == ActiveCardNotificationMode.DeadAir)
                    EvaluateDeadAirAlertOnMainThread(_deadAirAlertGeneration);

                EnsureTimerRunning();
                return StartCommandResult.RedeliverIntent;
            }
            catch (Exception ex)
            {
                return StopForInvalidRequest(startId, "Could not apply notification request.", ex);
            }
        }

        private static bool TryReadMode(Intent? intent, out ActiveCardNotificationMode mode)
        {
            mode = ActiveCardNotificationMode.None;
            if (intent == null || !intent.HasExtra(ExtraNotificationMode))
                return false;

            var rawMode = intent.GetIntExtra(ExtraNotificationMode, -1);
            if (!Enum.IsDefined(typeof(ActiveCardNotificationMode), rawMode))
                return false;

            mode = (ActiveCardNotificationMode)rawMode;
            return mode is ActiveCardNotificationMode.ActiveCard or ActiveCardNotificationMode.DeadAir;
        }

        private bool TryApplyActiveCard(Intent intent, IClock clock)
        {
            if (intent.HasExtra(ExtraDeadAirStartedAtUtc)
                || intent.HasExtra(ExtraDeadAirAlertNoiseRequested))
                return false;

            var wrapperJson = intent.GetStringExtra(ExtraCardJson);
            if (string.IsNullOrWhiteSpace(wrapperJson))
                return false;

            var wrapper = JsonSerializer.Deserialize<ActiveCardModelWrapper>(wrapperJson);
            if (wrapper == null || string.IsNullOrWhiteSpace(wrapper.Type))
                return false;

            var concreteType = Type.GetType(wrapper.Type, throwOnError: false);
            if (concreteType == null || !typeof(IActiveCardModel).IsAssignableFrom(concreteType))
                return false;

            if (wrapper.Data.Deserialize(concreteType) is not IActiveCardModel activeCard)
                return false;

            StopDeadAirAlertRuntime();
            _notificationMode = ActiveCardNotificationMode.None;
            _deadAirStartedAtUtc = null;
            _deadAirAlertNoiseRequested = false;

            if (_activeCard == null || _activeCard.Id != activeCard.Id)
            {
                _timerToTriggerExecuted = false;
                _earnedThisSession.Clear();
            }

            _activeCard = activeCard;
            _activeDayStartLocal = clock.LocalNow.Date;

            var intentSessionId = intent.GetStringExtra(ExtraSessionId);
            var isRestoredAfterRestart = string.IsNullOrEmpty(intentSessionId)
                || intentSessionId != _sessionId;

            if (isRestoredAfterRestart)
            {
                _achievementsSeededForCurrentCard = false;
                TriggerAchievementRefreshOnce(activeCard);
            }
            else
            {
                _achievementsSeededForCurrentCard = true;
            }

            _notificationMode = ActiveCardNotificationMode.ActiveCard;
            return true;
        }

        private bool TryApplyDeadAir(Intent intent, bool isRedelivery, IClock clock)
        {
            if (intent.HasExtra(ExtraCardJson)
                || intent.HasExtra(ExtraSessionId))
                return false;

            var serializedStart = intent.GetStringExtra(ExtraDeadAirStartedAtUtc);
            if (!StrictTimeSerializer.TryParseUtcInstant(serializedStart, out var startedAtUtc))
                return false;

            if (!TryReadOptionalBooleanExtra(
                    intent,
                    ExtraDeadAirAlertNoiseRequested,
                    out var alertNoiseRequested))
            {
                return false;
            }

            var sameConfiguration =
                _notificationMode == ActiveCardNotificationMode.DeadAir
                && _deadAirStartedAtUtc == startedAtUtc
                && _deadAirAlertNoiseRequested == alertNoiseRequested;

            if (!sameConfiguration)
            {
                StopDeadAirAlertRuntime();

                _deadAirAlertElapsedAtAnchor =
                    ActiveCardNotificationElapsedFormatter.CalculateElapsed(
                        startedAtUtc,
                        clock.UtcNow);
                _deadAirAlertAnchorElapsedRealtimeMilliseconds =
                    global::Android.OS.SystemClock.ElapsedRealtime();

                if (_deadAirAlertStateStore?.TryRead(
                        startedAtUtc,
                        out var restoredMilestones,
                        out var wasEligible) == true)
                {
                    // Only Android's START_REDELIVER_INTENT recovery may retain
                    // eligibility across process death. A normal request can be
                    // a deliberate stop/re-enable in the same Dead Air interval,
                    // so it must re-arm without backfilling expired one-shots.
                    _deadAirAlertState = isRedelivery
                        ? DeadAirAlertState.Restore(restoredMilestones, wasEligible)
                        : DeadAirAlertState.Initial(restoredMilestones);
                }
                else
                {
                    _deadAirAlertState = DeadAirAlertState.Initial();
                }

            }

            _notificationMode = ActiveCardNotificationMode.None;
            _activeCard = null;
            _activeDayStartLocal = default;
            _deadAirStartedAtUtc = startedAtUtc;
            _deadAirAlertNoiseRequested = alertNoiseRequested;
            _timerToTriggerExecuted = false;
            _achievementsSeededForCurrentCard = true;
            _earnedThisSession.Clear();
            _notificationMode = ActiveCardNotificationMode.DeadAir;
            return true;
        }

        private static bool TryReadOptionalBooleanExtra(
            Intent intent,
            string key,
            out bool value)
        {
            value = false;
            if (!intent.HasExtra(key))
                return true;

#pragma warning disable CA1422
            if (intent.Extras?.Get(key) is not Java.Lang.Boolean boxed)
#pragma warning restore CA1422
                return false;

            value = boxed.BooleanValue();
            return true;
        }

        private void EnsureTimerRunning()
        {
            if (_cts is { IsCancellationRequested: false })
                return;

            _cts = new CancellationTokenSource();
            _timerTask = RunTimerAsync(_cts.Token);
            _timerTask.Forget("Active card foreground timer");
        }

        private StartCommandResult StopForInvalidRequest(
            int startId,
            string message,
            Exception? exception = null)
        {
            System.Diagnostics.Debug.WriteLine(
                exception == null
                    ? $"Active card foreground service stopped: {message}"
                    : $"Active card foreground service stopped: {message} {exception}");

            ClearNotificationState(cancelTimer: true);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                StopForeground(StopForegroundFlags.Remove);
            else
#pragma warning disable CA1422
                StopForeground(true);
#pragma warning restore CA1422

            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }

        private void ClearNotificationState(bool cancelTimer)
        {
            StopDeadAirAlertRuntime();
            _notificationMode = ActiveCardNotificationMode.None;
            _activeCard = null;
            _activeDayStartLocal = default;
            _deadAirStartedAtUtc = null;
            _deadAirAlertNoiseRequested = false;
            _deadAirAlertState = DeadAirAlertState.Initial();
            _deadAirAlertElapsedAtAnchor = TimeSpan.Zero;
            _deadAirAlertAnchorElapsedRealtimeMilliseconds = 0;
            Interlocked.Exchange(ref _deadAirAlertEvaluationPosted, 0);
            _currentForegroundNotification = null;
            _timerToTriggerExecuted = false;
            _achievementsSeededForCurrentCard = true;
            _earnedThisSession.Clear();

            if (!cancelTimer)
                return;

            var cts = _cts;
            var timerTask = _timerTask;
            _cts = null;
            _timerTask = null;
            cts?.Cancel();
            timerTask?.Forget("Active card foreground timer shutdown");
        }

        private void QueueDeadAirAlertEvaluation(int generation)
        {
            var handler = _mainHandler;
            if (handler == null
                || Interlocked.Exchange(ref _deadAirAlertEvaluationPosted, 1) != 0)
            {
                return;
            }

            handler.Post(() =>
            {
                Interlocked.Exchange(ref _deadAirAlertEvaluationPosted, 0);
                EvaluateDeadAirAlertOnMainThread(generation);
            });
        }

        private void EvaluateDeadAirAlertOnMainThread(int generation)
        {
            if (generation != _deadAirAlertGeneration
                || _notificationMode != ActiveCardNotificationMode.DeadAir
                || _deadAirStartedAtUtc is not { } startedAtUtc)
            {
                return;
            }

            var elapsed = CalculateDeadAirAlertElapsed();
            var notificationVisible = ActiveCardNotificationVisibility.IsVisible(
                this,
                NotificationChannelId,
                NotificationId);
            var previousState = _deadAirAlertState;
            var decision = DeadAirAlertPolicy.Evaluate(
                previousState,
                elapsed,
                _deadAirAlertNoiseRequested,
                notificationVisible);

            _deadAirAlertState = decision.State;
            var statePersisted = true;
            if (decision.MilestonesChanged
                || previousState.WasEligible != decision.State.WasEligible)
            {
                statePersisted = _deadAirAlertStateStore?.Write(
                                     startedAtUtc,
                                     decision.State.Milestones,
                                     decision.State.WasEligible) == true;

                if (!statePersisted)
                    _deadAirAlertState = previousState;
            }

            if (decision.State.WasEligible && elapsed < DeadAirAlertPolicy.LoopThreshold)
            {
                _deadAirCountdownWakeLock?.AcquireUntilContinuousThreshold(elapsed);
                TryEnsureDeadAirAlertSoundController();
            }
            else
            {
                _deadAirCountdownWakeLock?.Release();
            }

            if (!statePersisted
                && decision.AudioCommand is not DeadAirAlertAudioCommand.StopAudio)
            {
                return;
            }

            switch (decision.AudioCommand)
            {
                case DeadAirAlertAudioCommand.PlayShortCue:
                    TryPlayDeadAirCue(DeadAirAlertCue.Short, generation);
                    break;

                case DeadAirAlertAudioCommand.PlayLongCue:
                    TryPlayDeadAirCue(DeadAirAlertCue.Long, generation);
                    break;

                case DeadAirAlertAudioCommand.StartLoop:
                    EnsureDeadAirAlertLoop(generation);
                    break;

                case DeadAirAlertAudioCommand.StopAudio:
                    StopDeadAirAudioForCurrentGeneration();
                    break;

                case DeadAirAlertAudioCommand.None:
                    if (decision.State.IsLoopRequested)
                        EnsureDeadAirAlertLoop(generation);
                    break;
            }
        }

        private TimeSpan CalculateDeadAirAlertElapsed()
        {
            var nowElapsedRealtimeMilliseconds = global::Android.OS.SystemClock.ElapsedRealtime();
            var elapsedSinceAnchorMilliseconds = Math.Max(
                0L,
                nowElapsedRealtimeMilliseconds
                    - _deadAirAlertAnchorElapsedRealtimeMilliseconds);

            try
            {
                return _deadAirAlertElapsedAtAnchor
                    + TimeSpan.FromMilliseconds(elapsedSinceAnchorMilliseconds);
            }
            catch (OverflowException)
            {
                return TimeSpan.MaxValue;
            }
        }

        private bool TryEnsureDeadAirAlertSoundController()
        {
            if (_deadAirAlertSoundController != null)
                return true;

            if (_mainHandler == null)
                return false;

            try
            {
                _deadAirAlertSoundController = new DeadAirAlertSoundController(
                    this,
                    _mainHandler,
                    OnDeadAirOneShotCompleted,
                    OnDeadAirContinuousPlaybackChanged);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not initialize Dead Air alert audio: {ex}");
                return false;
            }
        }

        private void TryPlayDeadAirCue(DeadAirAlertCue cue, int generation)
        {
            if (!TryEnsureDeadAirAlertSoundController())
                return;

            try
            {
                _deadAirAlertSoundController?.TryPlayOneShot(cue, generation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not play Dead Air alert cue: {ex}");
                StopDeadAirAudioForCurrentGeneration();
            }
        }

        private void EnsureDeadAirAlertLoop(int generation)
        {
            if (!TryEnsureDeadAirAlertSoundController())
                return;

            try
            {
                _deadAirAlertSoundController?.EnsureLoop(generation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not start Dead Air alert loop: {ex}");
                StopDeadAirAudioForCurrentGeneration();
            }
        }

        private void OnDeadAirOneShotCompleted(int generation)
        {
            if (generation != _deadAirAlertGeneration
                || _deadAirAlertState.IsLoopRequested)
            {
                return;
            }

            SetForegroundAudioMode(enabled: false);
        }

        private void OnDeadAirContinuousPlaybackChanged(int generation, bool isPlaying)
        {
            if (generation != _deadAirAlertGeneration)
                return;

            if (isPlaying
                && (_notificationMode != ActiveCardNotificationMode.DeadAir
                    || !_deadAirAlertState.IsLoopRequested))
            {
                _deadAirAlertSoundController?.StopAll(_deadAirAlertGeneration);
                return;
            }

            SetForegroundAudioMode(isPlaying);
        }

        private void StopDeadAirAudioForCurrentGeneration()
        {
            _deadAirAlertSoundController?.StopAll(_deadAirAlertGeneration);
            SetForegroundAudioMode(enabled: false);
        }

        private void StopDeadAirAlertRuntime()
        {
            unchecked
            {
                _deadAirAlertGeneration++;
            }

            _deadAirAlertSoundController?.StopAll(_deadAirAlertGeneration);
            _deadAirCountdownWakeLock?.Release();
            SetForegroundAudioMode(enabled: false);
        }

        #region Achevement Seeding logic

        private void TriggerAchievementRefreshOnce(IActiveCardModel acm)
        {
            // Try to acquire the "refresh lock"
            if (Interlocked.CompareExchange(
                    ref _achievementRefreshInProgress,
                    1,   // set to 1
                    0    // only if currently 0
                ) != 0)
            {
                // Someone else already triggered it
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await EnsureSeededAsync(acm);
                    _achievementsSeededForCurrentCard = true;
                }
                catch (Exception ex)
                {
                    // Log and FAIL CLOSED
                    // Achievements remain disabled until next restart
                    System.Diagnostics.Debug.WriteLine(
                        $"Achievement refresh failed: {ex}"
                    );
                }
                finally
                {
                    Interlocked.Exchange(ref _achievementRefreshInProgress, 0);
                }
            }).Forget("Achievement refresh");
        }


        private async Task EnsureSeededAsync(IActiveCardModel acm)
        {
            var achievements = ServiceHelper.GetService<IAchievementService>();
            acm.TimeValueAchievementEvaluators = await achievements.RefreshEvaluatorsAsync(acm.TimeValueAchievementEvaluators);
        }

        #endregion

        // Timer tick logic for both active-card and Dead Air modes.
        private async Task RunTimerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var clock = ServiceHelper.GetService<IClock>();

                    if (_notificationMode == ActiveCardNotificationMode.ActiveCard
                        && _activeCard is { } activeCard)
                    {
                        TickActiveCard(clock, activeCard);
                    }
                    else if (_notificationMode == ActiveCardNotificationMode.DeadAir
                             && _deadAirStartedAtUtc is { } deadAirStartedAtUtc)
                    {
                        var nowUtc = clock.UtcNow;
                        var elapsed = ActiveCardNotificationElapsedFormatter.CalculateElapsed(
                            deadAirStartedAtUtc,
                            nowUtc);
                        UpdateNotification(
                            "Dead Air",
                            ActiveCardNotificationElapsedFormatter.Format(elapsed),
                            openActiveCard: false);
                        QueueDeadAirAlertEvaluation(_deadAirAlertGeneration);
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"Active card foreground timer tick failed: {ex}");
                }

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    // Expected when the service/app is shutting down - safe to ignore.
                }

            }
        }

        private void TickActiveCard(IClock clock, IActiveCardModel activeCard)
        {
            var nowLocal = clock.LocalNow;
            var elapsed = activeCard.GetActiveTime(_activeDayStartLocal, nowLocal);
            UpdateNotification(
                activeCard.Title,
                ActiveCardNotificationElapsedFormatter.Format(elapsed),
                openActiveCard: true);

            if (activeCard is TatCardModel tat && tat.TargetActiveTime.HasValue)
            {
                var targetSeconds = tat.TargetActiveTime.Value.TotalSeconds;
                var elapsedSeconds = elapsed.TotalSeconds;
                var targetReached = elapsedSeconds >= targetSeconds - 1
                    && elapsedSeconds <= targetSeconds + 1;

                if (!_timerToTriggerExecuted && targetReached)
                {
                    _timerToTriggerExecuted = true;
                    ShowTargetTimeReachedNotification(tat, elapsed);
                }
            }

            if (!_achievementsSeededForCurrentCard
                || activeCard.TimeValueAchievementEvaluators.Count == 0)
            {
                return;
            }

            foreach (var evaluator in activeCard.TimeValueAchievementEvaluators)
            {
                var earnedAchievements = evaluator.CheckForEarnedAchievements(
                    1,
                    activeCard.ValuePerMinute / 60,
                    nowLocal);

                if (earnedAchievements == null || earnedAchievements.Count == 0)
                    continue;

                PersistEarnedAchievementsAsync(earnedAchievements)
                    .Forget("Persist earned achievements");
                ShowAchievementEarnedNotification(earnedAchievements);
            }
        }

        #region Alert notification channel (for Activity Timer alert)

        void CreateAlertNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager == null)
                return;

            var existing = manager.GetNotificationChannel(AlertChannelId);
            if (existing != null)
                return;

            var channel = new NotificationChannel(
                AlertChannelId,
                AlertChannelName,
                NotificationImportance.High // ?? high => heads-up, sound by default
            )
            {
                Description = "Alerts when the active card reaches its target time.",
                LockscreenVisibility = NotificationVisibility.Public
            };

            channel.EnableVibration(true);
            channel.SetVibrationPattern(new long[] { 0, 500, 300, 500 });

            // Optional: make sure it uses the default notification sound explicitly
            var uri = am.RingtoneManager.GetDefaultUri(am.RingtoneType.Notification);
            var audioAttrs = new am.AudioAttributes.Builder()
                .SetUsage(am.AudioUsageKind.Notification)
                .SetContentType(am.AudioContentType.Sonification)
                .Build();
            channel.SetSound(uri, audioAttrs);

            manager.CreateNotificationChannel(channel);
        }

        private void ShowTargetTimeReachedNotification(TatCardModel? tat, TimeSpan elapsed)
        {
            CreateAlertNotificationChannel();

            string title = tat?.Title ?? "Target time reached";
            string message = $"You reached the target time: {elapsed:hh\\:mm\\:ss}";

            Intent launchIntent = PackageManager!.GetLaunchIntentForPackage(PackageName)!;
            launchIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            PendingIntent pendingIntent = PendingIntent.GetActivity(
                this,
                0,
                launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            );

            var builder = new NotificationCompat.Builder(this, AlertChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(Resource.Drawable.ic_m3_chip_close)
                .SetAutoCancel(true)
                .SetOngoing(false)
                .SetVisibility((int)NotificationVisibility.Public)
                .SetPriority((int)NotificationPriority.High)
                .SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate))
                .SetContentIntent(pendingIntent);

            var nm = NotificationManagerCompat.From(this);
            nm.Notify(AlertNotificationId, builder.Build());
        }

        #endregion

        #region Silent Notification Channel (for active card notification)
        void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager == null)
                return;

            var existing = manager.GetNotificationChannel(NotificationChannelId);
            if (existing != null)
                return;

            var channel = new NotificationChannel(
                NotificationChannelId,
                NotificationChannelName,
                NotificationImportance.Low // Low so it does not ping constantly
            )
            {
                Description = "Shows the currently active card."
            };

            // Make it visible on lock screen
            channel.LockscreenVisibility = NotificationVisibility.Public;

            manager.CreateNotificationChannel(channel);
        }

        private Notification BuildCurrentNotification(IClock clock)
        {
            return _notificationMode switch
            {
                ActiveCardNotificationMode.ActiveCard when _activeCard is { } activeCard =>
                    BuildNotification(
                        activeCard.Title,
                        ActiveCardNotificationElapsedFormatter.Format(
                            activeCard.GetActiveTime(_activeDayStartLocal, clock.LocalNow)),
                        openActiveCard: true),

                ActiveCardNotificationMode.DeadAir when _deadAirStartedAtUtc is { } startedAtUtc =>
                    BuildNotification(
                        "Dead Air",
                        ActiveCardNotificationElapsedFormatter.Format(
                            ActiveCardNotificationElapsedFormatter.CalculateElapsed(
                                startedAtUtc,
                                clock.UtcNow)),
                        openActiveCard: false),

                _ => throw new InvalidOperationException(
                    "Cannot build an ongoing notification without a valid notification state.")
            };
        }

        private Notification BuildNotification(
            string title,
            string contentText,
            bool openActiveCard)
        {
            var builder = new NotificationCompat.Builder(this, NotificationChannelId)
                .SetContentTitle(title)
                .SetContentText(contentText)
                .SetSmallIcon(Resource.Drawable.ic_m3_chip_close)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .SetContentIntent(BuildContentPendingIntent(openActiveCard))
                .SetVisibility((int)NotificationVisibility.Public)
                .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
                .SetOnlyAlertOnce(true);

            return builder.Build();
        }

        private PendingIntent? BuildContentPendingIntent(bool openActiveCard)
        {
            var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName);
            if (launchIntent == null)
                return null;

            launchIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            if (openActiveCard && _activeCard?.CardID > 0)
            {
                launchIntent.SetAction(ActionOpenActiveCard);
                launchIntent.PutExtra(ExtraOpenActiveCard, true);
                launchIntent.PutExtra(ExtraTargetCardId, _activeCard.CardID);
            }
            else
            {
                launchIntent.SetAction(ActionOpenHome);
                launchIntent.RemoveExtra(ExtraOpenActiveCard);
                launchIntent.RemoveExtra(ExtraTargetCardId);
            }

            return PendingIntent.GetActivity(
                this,
                NotificationId,
                launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }

        private void UpdateNotification(string title, string contentText, bool openActiveCard)
        {
            var builder = new NotificationCompat.Builder(this, NotificationChannelId)
                .SetContentTitle(title)
                .SetContentText(contentText)
                .SetSmallIcon(Resource.Drawable.ic_m3_chip_close)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .SetContentIntent(BuildContentPendingIntent(openActiveCard))
                .SetVisibility((int)NotificationVisibility.Public)
                .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
                .SetOnlyAlertOnce(true);

            _currentForegroundNotification = builder.Build();
            var nm = NotificationManagerCompat.From(this);
            nm.Notify(NotificationId, _currentForegroundNotification);
        }

        private void SetForegroundAudioMode(bool enabled)
        {
            if (_foregroundIncludesMediaPlayback == enabled)
                return;

            _foregroundIncludesMediaPlayback = enabled;
            if (_currentForegroundNotification != null)
                StartOrUpdateForeground(enabled);
        }

        private void StartOrUpdateForeground(bool includeMediaPlayback)
        {
            var notification = _currentForegroundNotification
                ?? throw new InvalidOperationException("A foreground notification must be built before promoting the service.");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
            {
                var type = ForegroundService.TypeSpecialUse;
                if (includeMediaPlayback)
                    type |= ForegroundService.TypeMediaPlayback;

                StartForeground(NotificationId, notification, type);
            }
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                var type = includeMediaPlayback
                    ? ForegroundService.TypeMediaPlayback
                    : ForegroundService.TypeNone;
                StartForeground(NotificationId, notification, type);
            }
            else
            {
                StartForeground(NotificationId, notification);
            }

            _foregroundIncludesMediaPlayback = includeMediaPlayback;
        }
        #endregion

        #region Alert II notification channel (for Achievements Earned notification)

        void CreateAchievementNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager == null)
                return;

            var existing = manager.GetNotificationChannel(AchievementChannelId);
            if (existing != null)
                return;

            var channel = new NotificationChannel(
                AchievementChannelId,
                AchievementChannelName,
                NotificationImportance.High
            )
            {
                Description = "Alerts when an achievement is earned.",
                LockscreenVisibility = NotificationVisibility.Public
            };

            // Vibration
            channel.EnableVibration(true);
            channel.SetVibrationPattern(new long[] { 0, 250, 150, 250, 150, 400 });

            // Sound (default notification sound)
            var uri = am.RingtoneManager.GetDefaultUri(am.RingtoneType.Notification);
            var audioAttrs = new am.AudioAttributes.Builder()
                .SetUsage(am.AudioUsageKind.Notification)
                .SetContentType(am.AudioContentType.Sonification)
                .Build();
            channel.SetSound(uri, audioAttrs);

            manager.CreateNotificationChannel(channel);
        }


        private void ShowAchievementEarnedNotification(List<AchievementCardModel> earnedAchievements)
        {
            if (earnedAchievements == null || earnedAchievements.Count == 0)
                return;

            CreateAchievementNotificationChannel();

            Intent launchIntent = PackageManager!.GetLaunchIntentForPackage(PackageName)!;
            launchIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            var pendingIntent = PendingIntent.GetActivity(
                this,
                0,
                launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            );

            var nm = NotificationManagerCompat.From(this);

            foreach (var ach in earnedAchievements)
            {
                // If you also want to prevent double-notifying in the same session,
                // you can rely on _earnedThisSession in PersistEarnedAchievementsAsync
                // OR add a guard here too.

                var title = "Achievement earned";
                var message = ach?.Title ?? "Achievement";

                // Stable unique ID per achievement (so repeated earns overwrite the prior one for that achievement)
                var notificationId = AchievementNotificationBaseId + Math.Abs(ach.Id % 5000);

                var builder = new NotificationCompat.Builder(this, AchievementChannelId)
                    .SetSmallIcon(Resource.Drawable.ic_m3_chip_close)
                    .SetContentTitle(title)
                    .SetContentText(message)
                    .SetContentIntent(pendingIntent)
                    .SetAutoCancel(true)
                    .SetVisibility((int)NotificationVisibility.Public)
                    .SetPriority((int)NotificationPriority.High);

                // For < Android 8, channel doesn't exist so set defaults for sound/vibrate here:
                if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                {
                    builder.SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate));
                }

                nm.Notify(notificationId, builder.Build());
            }
        }


        private async Task PersistEarnedAchievementsAsync(IEnumerable<AchievementCardModel> earned)
        {
            var achievements = ServiceHelper.GetService<IAchievementService>();
            var now = ServiceHelper.GetService<IClock>().UtcNow;

            foreach (var ach in earned)
            {
                if (!_earnedThisSession.Add(ach.Id)) continue;

                // Assuming AchievementCardModel has a numeric Id
                await achievements.MarkAchievementEarnedAsync(ach.Id, now);

                // Optional: update in-memory model too, if it has a LastEarnedAt property:
                ach.LastEarnedAt = now;
            }
        }


        #endregion

        #region DEBUG
        public void ForceCreateChannels(Context context)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
                if (manager == null) return;

                // Create main foreground channel
                var foregroundChannel = new NotificationChannel(
                    NotificationChannelId,
                    NotificationChannelName,
                    NotificationImportance.Low
                );
                manager.CreateNotificationChannel(foregroundChannel);

                // Create alert channel (sound & vibration)
                var alertChannel = new NotificationChannel(
                    AlertChannelId,
                    AlertChannelName,
                    NotificationImportance.High
                );
                alertChannel.EnableVibration(true);
                alertChannel.SetVibrationPattern(new long[] { 0, 300, 200, 300 });

                var uri = am.RingtoneManager.GetDefaultUri(am.RingtoneType.Notification);
                var attrs = new am.AudioAttributes.Builder()
                    .SetUsage(am.AudioUsageKind.Notification)
                    .SetContentType(am.AudioContentType.Sonification)
                    .Build();
                alertChannel.SetSound(uri, attrs);

                manager.CreateNotificationChannel(alertChannel);
            }
        }

        #endregion

    }
}
#endif
