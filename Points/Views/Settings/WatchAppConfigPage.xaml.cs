using Points.Services.Navigation;
using Points.Services.Watch;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class WatchAppConfigPage : ContentPage
{
    private readonly IAppNavigationService _navigation;

    public WatchAppConfigPage(
        IWatchShortcutSettingsService watchShortcuts,
        IWatchSnapshotPublishService watchSnapshots,
        IAppNavigationService navigation)
    {
        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        BindingContext = new WatchAppConfigViewModel(
            watchShortcuts,
            watchSnapshots,
            ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await _navigation.PopAsync();
    }
}
