using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Points.Platforms.Android;

namespace Points
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            RequestNotificationPermissionIfNeeded();

            new ActiveCardForegroundService().ForceCreateChannels(this);
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
