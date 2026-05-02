#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Points.Services.Backup;

namespace Points.Platforms.Android
{
    [Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = GoogleDriveOAuthDefaults.CallbackScheme,
        DataPath = GoogleDriveOAuthDefaults.CallbackPath)]
    public sealed class GoogleDriveWebAuthenticationCallbackActivity
        : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
    {
    }
}
#endif
