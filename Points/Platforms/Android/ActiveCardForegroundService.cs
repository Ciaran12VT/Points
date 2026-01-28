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

// Namespace: use your actual root namespace
namespace Points.Platforms.Android
{
    // The ForegroundServiceType = TypeDataSync is a reasonable generic type
    // for “app internal ongoing work” under Android 14’s rules.
    [Service(ForegroundServiceType = ForegroundService.TypeDataSync, Exported = false)]
    public class ActiveCardForegroundService : Service
    {
        public const string ExtraCardJson = "EXTRA_ACTIVE_CARD_JSON";

        const string NotificationChannelId = "points_active_card_channel";
        const string NotificationChannelName = "Active card";
        const int NotificationId = 1001;

        // 🔔 New: alert channel for noisy/vibrating notifications
        const string AlertChannelId = "points_active_card_alert_channel_v2";
        const string AlertChannelName = "Active card alerts";
        const int AlertNotificationId = 2001;

        private IActiveCardModel? _activeCard;
        private DateTime _startDate;
        private System.Threading.CancellationTokenSource? _cts;

        public override IBinder? OnBind(Intent? intent)
        {
            // We are not binding to this service, just starting it.
            return null;
        }

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
                            cardTitle = acm.Title;
                            _activeCard = acm;
                            _startDate = DateTime.Today;

                            if (_cts == null)
                            {
                                _cts = new CancellationTokenSource();
                                StartTimer(_cts.Token);
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
        private bool _timerToTriggerExecuted = false;
        private async void StartTimer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activeCard is not null && _activeCard != null)
                    {
                        var elapsed = _activeCard.GetActiveTime(_startDate, DateTime.Now);
                        string formatted = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
                        UpdateNotification(formatted);

                        if (_activeCard is TatCardModel tat && tat.TargetActiveTime.HasValue)
                        {
                            // NOTE: compare seconds to seconds (was TotalNanoseconds before)
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

        public override void OnDestroy()
        {
            _cts?.Cancel();
            _cts = null;
            base.OnDestroy();
        }

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
