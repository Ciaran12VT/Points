using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Points.Helpers;
using Points.Models;
using Points.Platforms.Android;
using Points.Services;
using Points.Services.Diagnostics;
using Points.Services.MissionSharing;
using Points.Services.Navigation;
using Points.Services.Watch;

namespace Points
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault },
        DataScheme = "content",
        DataMimeType = MissionShareFileTypes.ContentType)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault },
        DataScheme = "file",
        DataPathPattern = @".*\.pmj",
        DataMimeType = "*/*")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault },
        DataScheme = "content",
        DataPathPattern = @".*\.pmj",
        DataMimeType = "*/*")]
    public class MainActivity : MauiAppCompatActivity
    {
        private string? _lastMissionShareIntentKey;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            RequestNotificationPermissionIfNeeded();

            new ActiveCardForegroundService().ForceCreateChannels(this);
            ResolveService<IWatchBridge>().StartAsync().Forget("Watch bridge startup");

            HandleMissionShareIntent(Intent);
            HandleHomeNotificationIntent(Intent);
            HandleActiveCardNotificationIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);

            if (intent != null)
                Intent = intent;

            HandleMissionShareIntent(intent);
            HandleHomeNotificationIntent(intent);
            HandleActiveCardNotificationIntent(intent);
        }

        private static void HandleHomeNotificationIntent(Intent? intent)
        {
            if (!string.Equals(
                    intent?.Action,
                    ActiveCardForegroundService.ActionOpenHome,
                    StringComparison.Ordinal))
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TaskSupervisor.Forget(
                    ReturnHomeFromNotificationAsync(),
                    "Open Home from Dead Air notification");
            });
        }

        private static async Task ReturnHomeFromNotificationAsync()
        {
            try
            {
                await ResolveService<IAppNavigationService>().PopToRootAsync();
            }
            catch (InvalidOperationException ex)
            {
                // A cold launch is already headed to Home; Shell may not be mounted yet.
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void HandleActiveCardNotificationIntent(Intent? intent)
        {
            if (intent == null)
                return;

            var shouldOpenActiveCard =
                intent.GetBooleanExtra(ActiveCardForegroundService.ExtraOpenActiveCard, false) ||
                string.Equals(
                    intent.Action,
                    ActiveCardForegroundService.ActionOpenActiveCard,
                    StringComparison.Ordinal);

            if (!shouldOpenActiveCard)
                return;

            var cardId = intent.GetLongExtra(ActiveCardForegroundService.ExtraTargetCardId, 0);
            if (cardId <= 0)
                return;

            ResolveService<IActiveCardNotificationNavigationService>()
                .RequestNavigation(cardId);
        }

        private static T ResolveService<T>() where T : notnull
        {
            return ServiceHelper.GetService<T>();
        }

        private void HandleMissionShareIntent(Intent? intent)
        {
            if (intent?.Action != Intent.ActionView || intent.Data == null)
                return;

            if (!LooksLikeMissionShare(intent))
                return;

            var key = $"{intent.Data}|{intent.Type}";
            if (string.Equals(_lastMissionShareIntentKey, key, StringComparison.Ordinal))
                return;

            _lastMissionShareIntentKey = key;

            _ = Task.Run(async () =>
            {
                try
                {
                    var localPath = await CopyMissionShareToCacheAsync(intent.Data);

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await ServiceHelper
                            .GetService<IMissionShareLaunchHandler>()
                            .OpenImportPageAsync(localPath);
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            });
        }

        private static bool LooksLikeMissionShare(Intent intent)
        {
            if (string.Equals(intent.Type, MissionShareFileTypes.ContentType, StringComparison.OrdinalIgnoreCase))
                return true;

            var path = intent.Data?.Path ?? intent.Data?.LastPathSegment ?? "";
            return path.EndsWith(MissionShareFileTypes.Extension, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> CopyMissionShareToCacheAsync(Android.Net.Uri uri)
        {
            var directory = Path.Combine(FileSystem.CacheDirectory, "IncomingMissionShares");
            Directory.CreateDirectory(directory);

            var targetPath = Path.Combine(directory, $"{Guid.NewGuid():N}{MissionShareFileTypes.Extension}");

            await using var source = OpenMissionShareStream(uri);
            await using var target = System.IO.File.Create(targetPath);
            await source.CopyToAsync(target);

            return targetPath;
        }

        private Stream OpenMissionShareStream(Android.Net.Uri uri)
        {
            if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(uri.Path))
            {
                return System.IO.File.OpenRead(uri.Path);
            }

            return ContentResolver?.OpenInputStream(uri)
                ?? throw new InvalidOperationException("Could not read the selected mission share file.");
        }

        private void RequestNotificationPermissionIfNeeded()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) // Android 13 (API 33)
            {
                // Already granted?
                if (CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
                {
                    RequestPermissions(
                        new[] { Manifest.Permission.PostNotifications },
                        requestCode: 1001
                    );
                }
            }
        }
    }
}
