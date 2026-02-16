#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Android.Graphics;
using Android.Content.PM;
using System.Text.Json;
using Points.Models;
using Java.Sql;
using am = Android.Media;
using System.Threading;
using Points.Helpers;
using Points.Services;

// Namespace: use your actual root namespace
namespace Points.Platforms.Android
{
    // The ForegroundServiceType = TypeDataSync is a reasonable generic type
    // for “app internal ongoing work” under Android 14’s rules.
    [Service(ForegroundServiceType = ForegroundService.TypeDataSync, Exported = false)]
    public class ActiveCardForegroundService : Service
    {
        //Intent Extras
        public const string ExtraCardJson = "EXTRA_ACTIVE_CARD_JSON";
        public const string ExtraSessionId = "EXTRA_SERVICE_SESSION_ID";

        //Channel meta-data
        const string NotificationChannelId = "points_active_card_channel";
        const string NotificationChannelName = "Active card";
        const int NotificationId = 1001;

        // 🔔 New: alert channel for noisy/vibrating notifications
        const string AlertChannelId = "points_active_card_alert_channel_v2";
        const string AlertChannelName = "Active card alerts";
        const int AlertNotificationId = 2001;

        // 🏆 Achievement alert channel (noisy)
        const string AchievementChannelId = "points_achievement_alert_channel_v1";
        const string AchievementChannelName = "Achievements";
        const int AchievementNotificationBaseId = 3000;

        //Active Card
        private IActiveCardModel? _activeCard;
        private DateTime _startDate;
        private System.Threading.CancellationTokenSource? _cts;

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
        }

        public override void OnDestroy()
        {
            _cts?.Cancel();
            _cts = null;
            base.OnDestroy();
        }

        public override IBinder? OnBind(Intent? intent) => null;

        #endregion

        //Forground Service entry-point for extracting intents and kicking off timer
        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            // Read the active card title from the Intent extras
            var cardTitle = "No active card";

            var cardModelWrapperJson = intent?.GetStringExtra(ExtraCardJson);
            if(!string.IsNullOrEmpty(cardModelWrapperJson))
            {
                var cardModelWrapper = JsonSerializer.Deserialize<ActiveCardModelWrapper>(cardModelWrapperJson);
                if(cardModelWrapper != null)
                {
                    var concreteType = Type.GetType(cardModelWrapper.Type);
                    var instance = cardModelWrapper.Data.Deserialize(concreteType);

                    if(instance != null)
                    {
                        if(instance is IActiveCardModel acm)
                        {
                            if(_activeCard is null || (_activeCard is not null && _activeCard.Id != acm.Id))
                            {
                                _timerToTriggerExecuted = false;
                                _earnedThisSession.Clear();
                                //Interlocked.Exchange(ref _achievementRefreshInProgress, 0);
                            }

                            cardTitle = acm.Title;
                            _activeCard = acm;
                            _startDate = DateTime.Today;

                            if (_cts == null)
                            {
                                _cts = new CancellationTokenSource();
                                StartTimer(_cts.Token);
                            }

                            var intentSessionId = intent?.GetStringExtra(ExtraSessionId);

                            var isRestoredAfterRestart = string.IsNullOrEmpty(intentSessionId) || intentSessionId != _sessionId;

                            if (isRestoredAfterRestart)
                            {
                                // ✅ do your one-time DB recrunch for the evaluations in this active card
                                _achievementsSeededForCurrentCard = false;
                                TriggerAchievementRefreshOnce(acm);
                            }
                            else
                            {
                                _achievementsSeededForCurrentCard = true;
                            }
                        }
                    }
                }
            }

            // Ensure notification channel exists (Android 8+)
            CreateNotificationChannel();

            // Build the ongoing notification
            var notification = BuildNotification(cardTitle);

            // On Android 10+ there is an overload that also takes the foreground service type.
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                StartForeground(NotificationId, notification, ForegroundService.TypeDataSync);
            }
            else
            {
                StartForeground(NotificationId, notification);
            }

            return StartCommandResult.RedeliverIntent;

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

            _ = Task.Run(async () =>
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
            });
        }


        private async Task EnsureSeededAsync(IActiveCardModel acm)
        {
            var db = ServiceHelper.GetService<IDbService>();
            acm.TimeValueAchievementEvaluators = await db.RefreshEvaluatorsAsync(acm.TimeValueAchievementEvaluators);
        }

        #endregion

        //Timer tick logic is in here
        private async void StartTimer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activeCard is not null)
                    {
                        //Update notification time
                        var elapsed = _activeCard.GetActiveTime(_startDate, DateTime.Now);
                        string formatted = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
                        UpdateNotification(formatted);

                        //Check if there is a timer associated with the card and check if it should be triggered
                        if (_activeCard is TatCardModel tat && tat.TargetActiveTime.HasValue)
                        {
                            var targetSeconds = tat.TargetActiveTime.Value.TotalSeconds;
                            var elapsedSeconds = elapsed.TotalSeconds;

                            bool timerToTrigger =
                                elapsedSeconds >= targetSeconds - 1 &&
                                elapsedSeconds <= targetSeconds + 1;

                            if (!_timerToTriggerExecuted && timerToTrigger)
                            {
                                _timerToTriggerExecuted = true;
                                ShowTargetTimeReachedNotification(tat, elapsed);
                            }
                        }

                        // Check achevements
                        if(_achievementsSeededForCurrentCard && _activeCard.TimeValueAchievementEvaluators.Count > 0)
                        {
                            foreach (var evaluator in _activeCard.TimeValueAchievementEvaluators)
                            {
                                var earnedAchievements = evaluator.CheckForEarnedAchievements(1, _activeCard.ValuePerMinute / 60);

                                if(earnedAchievements != null && earnedAchievements.Count > 0)
                                {
                                    _ = PersistEarnedAchievementsAsync(earnedAchievements);

                                    ShowAchievementEarnedNotification(earnedAchievements);
                                }
                            }
                        }                      
                    }
                }
                catch
                {
                    // swallow tick errors to avoid service crash
                }

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    // Expected when the service/app is shutting down – safe to ignore.
                }

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
                NotificationImportance.High // 🔊 high => heads-up, sound by default
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
                NotificationImportance.Low // Low so it doesn’t ping constantly
            )
            {
                Description = "Shows the currently active card."
            };

            // Make it visible on lock screen
            channel.LockscreenVisibility = NotificationVisibility.Public;

            manager.CreateNotificationChannel(channel);
        }

        Notification BuildNotification(string cardTitle)
        {
            // PendingIntent to reopen the app when the notification is tapped.
            // For now it just brings the app to the foreground; later we’ll add card-specific logic.
            Intent launchIntent = PackageManager!.GetLaunchIntentForPackage(PackageName)!;
            launchIntent?.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            PendingIntent? pendingIntent = PendingIntent.GetActivity(
                this,
                0,
                launchIntent!,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            );

            var builder = new NotificationCompat.Builder(this, NotificationChannelId)
                .SetContentTitle(cardTitle)
                .SetContentText("Current Activity")
                .SetSmallIcon(Resource.Drawable.ic_m3_chip_close) // we’ll talk about this below
                .SetOngoing(true) // makes it “persistent”
                .SetAutoCancel(false)
                .SetContentIntent(pendingIntent)
                // Visible on lock screen; you can tweak if you want it hidden
                .SetVisibility((int)NotificationVisibility.Public)
                .SetOnlyAlertOnce(true); // don’t re-sound each update

            return builder.Build();
        }

        private void UpdateNotification(string contentText)
        {
            var builder = new NotificationCompat.Builder(this, NotificationChannelId)
                .SetContentTitle(_activeCard?.Title ?? "Active card")
                .SetContentText(contentText)
                .SetSmallIcon(Resource.Drawable.ic_m3_chip_close)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .SetVisibility((int)NotificationVisibility.Public)
                .SetOnlyAlertOnce(true);

            var nm = NotificationManagerCompat.From(this);
            nm.Notify(NotificationId, builder.Build());
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
            var db = ServiceHelper.GetService<IDbService>();
            var now = DateTime.Now;

            foreach (var ach in earned)
            {
                if (!_earnedThisSession.Add(ach.Id)) continue;

                // Assuming AchievementCardModel has a numeric Id
                await db.MarkAchievementEarnedAsync(ach.Id, now);

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
