using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Achievements;

namespace Points.Views.Achievements;

public partial class TrophyViewerPage : ContentPage
{
    public TrophyViewerPage(
        TrophyModel trophy,
        IAchievementService achievements,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
	{
		InitializeComponent();

        BindingContext = new TrophyViewerViewModel(
            trophy,
            achievements,
            navigation,
            dialogs,
            clock);
    }
}
