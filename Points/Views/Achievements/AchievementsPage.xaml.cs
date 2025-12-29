using Points.ViewModels;

namespace Points.Views.Achievements;

public partial class AchievementsPage : ContentPage
{
	public AchievementsPage(List<string> availableTagsList)
	{
		InitializeComponent();

        BindingContext = new AchievementsViewModel(availableTagsList);
    }
}