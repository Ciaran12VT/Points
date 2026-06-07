using Points.Services.Navigation;
using Points.Views.Missions;

namespace Points.Services.MissionSharing;

public sealed class MissionShareLaunchHandler : IMissionShareLaunchHandler
{
    private readonly IMissionShareService _missionShares;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

    public MissionShareLaunchHandler(
        IMissionShareService missionShares,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        _missionShares = missionShares ?? throw new ArgumentNullException(nameof(missionShares));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    public async Task OpenImportPageAsync(string filePath)
    {
        try
        {
            var preview = await _missionShares.CreateImportPreviewAsync(filePath);
            var page = new MissionImportPage(preview, _missionShares, _navigation, _dialogs);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    await _navigation.PushAsync(page);
                    return;
                }
                catch (InvalidOperationException) when (attempt < 19)
                {
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Mission import failed", ex.Message, "OK");
        }
    }
}
