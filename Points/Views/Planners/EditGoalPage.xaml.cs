using Points.ViewModels;

namespace Points.Views.Planners;

public partial class EditGoalPage : ContentPage
{
    private readonly PlannerProgressRowVm _row;

    public EditGoalPage(PlannerProgressRowVm row)
	{
		InitializeComponent();
        _row = row;
        BindingContext = row; // edits the row directly
    }


    private async void Cancel_Clicked(object sender, EventArgs e)
    {
        // If you need true cancel-without-changes, use a cloned edit VM instead (see note below).
        await Navigation.PopModalAsync();
    }

    private async void Save_Clicked(object sender, EventArgs e)
    {
        // If you bound Entry to double directly and it worked, you're done.
        // Otherwise parse/validate here.
        await Navigation.PopModalAsync();
    }
}