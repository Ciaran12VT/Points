using Points.Helpers;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Achievements;

public partial class AchievementsPage : ContentPage
{

/* Unmerged change from project 'Points (net8.0-android)'
Before:
	public AchievementsPage(Services.IDbService _db, List<string> availableTagsList)
	{
After:
	public AchievementsPage(IDbService _db, List<string> availableTagsList)
	{
*/
	public AchievementsPage(Services.Sqlite.Interfaces.IDbService _db, List<string> availableTagsList)
	{
		InitializeComponent();

        BindingContext = new AchievementsViewModel(availableTagsList, _db, ServiceHelper.GetService<IClock>());
    }
}
