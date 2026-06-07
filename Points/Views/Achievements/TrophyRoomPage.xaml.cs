using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Achievements;

namespace Points.Views.Achievements;

public partial class TrophyRoomPage : ContentPage
{
    public TrophyRoomPage(
        IAchievementService achievements,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
	{
		InitializeComponent();

        BindingContext = new TrophyRoomViewModel(achievements, navigation, dialogs, clock);
    }
}
