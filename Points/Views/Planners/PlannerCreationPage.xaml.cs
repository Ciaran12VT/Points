using Points.Models;
using Points.Services;
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

        // Reset selection immediately so the row can be tapped again.
        RowsList.SelectedItem = null;

        // Open modal edit page
        await Navigation.PushModalAsync(new NavigationPage(new EditGoalPage(selected)));
    }

}