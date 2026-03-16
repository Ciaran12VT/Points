using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Planners;

public partial class PlannerCreationPage : ContentPage
{
	public PlannerCreationPage(IDbService db)
	{
		InitializeComponent();

		BindingContext = new PlannerCreationViewModel(db);

    }

    private async void RowsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection?.FirstOrDefault() as PlannerProgressRowVm;
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