using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Achievements;

public partial class TrophyRoomPage : ContentPage
{
    private IDbService _db;

    public TrophyRoomPage(IDbService db)
	{
		InitializeComponent();
        _db = db;

        BindingContext = new TrophyRoomViewModel(_db);
    }

    private async void OnViewTrophyClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.CommandParameter is not TrophyModel trophy) return;

        // Fullscreen modal viewer
        await Shell.Current.Navigation.PushModalAsync(new NavigationPage(new TrophyViewerPage(trophy, _db)));
    }
}