using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Achievements;

public partial class TrophyRoomPage : ContentPage
{
    private readonly IAchievementService _achievements;

    public TrophyRoomPage(IAchievementService achievements)
	{
		InitializeComponent();
        _achievements = achievements;

        BindingContext = new TrophyRoomViewModel(_achievements);
    }

    private async void OnViewTrophyClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.CommandParameter is not TrophyModel trophy) return;

        // Fullscreen modal viewer
        await Shell.Current.Navigation.PushModalAsync(new NavigationPage(new TrophyViewerPage(trophy, _achievements)));
    }
}
