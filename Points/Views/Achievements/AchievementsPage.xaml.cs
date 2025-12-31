using Points.ViewModels;

namespace Points.Views.Achievements;

public partial class AchievementsPage : ContentPage
{
	public AchievementsPage(Services.IDbService _db, List<string> availableTagsList)
	{
		InitializeComponent();

        BindingContext = new AchievementsViewModel(availableTagsList);
    }
}