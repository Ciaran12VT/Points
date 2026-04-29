using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Goals;

public partial class GoalCreationPage : ContentPage
{
	public GoalCreationPage(ICardReadService cardReader, IGoalService goals, IClock? clock = null)
	{
		InitializeComponent();

		BindingContext = new GoalCreationViewModel(cardReader, goals, clock);

    }

    private async void RowsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection?.FirstOrDefault() as GoalProgressRowVm;
        if (selected is null) return;

        RowsList.SelectedItem = null;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = new EditGoalPage(selected);
                await Navigation.PushModalAsync(new NavigationPage(page));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Crash during navigation", ex.ToString(), "OK");
        }
    }


}
