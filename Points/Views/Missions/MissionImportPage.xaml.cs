using Points.Models;
using Points.Services.MissionSharing;
using Points.Services.Navigation;
using Points.ViewModels.Missions;

namespace Points.Views.Missions;

public partial class MissionImportPage : ContentPage
{
    public MissionImportPage(
        MissionSharePreview preview,
        IMissionShareService missionShares,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();
        BindingContext = new MissionImportViewModel(preview, missionShares, navigation, dialogs);
    }
}
