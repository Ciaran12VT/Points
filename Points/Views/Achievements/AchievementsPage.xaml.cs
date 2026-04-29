using Points.Helpers;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Achievements;

public partial class AchievementsPage : ContentPage
{
	public AchievementsPage(ICardWriteService cardWriter, IAchievementService achievements, List<string> availableTagsList)
	{
		InitializeComponent();

        BindingContext = new AchievementsViewModel(
            availableTagsList,
            cardWriter,
            achievements,
            ServiceHelper.GetService<IClock>());
    }
}
