using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Achievements;

namespace Points.Views.Achievements;

public partial class AchievementsPage : ContentPage
{
	public AchievementsPage(
        ICardWriteService cardWriter,
        IAchievementService achievements,
        List<string> availableTagsList,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
	{
		InitializeComponent();

        BindingContext = new AchievementsViewModel(
            availableTagsList,
            cardWriter,
            achievements,
            navigation,
            dialogs,
            clock);
    }
}
