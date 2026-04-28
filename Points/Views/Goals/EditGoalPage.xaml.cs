using Points.ViewModels;

namespace Points.Views.Goals;

public partial class EditGoalPage : ContentPage
{
    private readonly GoalProgressRowVm _row;

    public EditGoalPage(GoalProgressRowVm row)
	{
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            MainThread.BeginInvokeOnMainThread(async () =>
                await Application.Current!.MainPage!.DisplayAlert("XAML load failed", ex.ToString(), "OK"));
            throw;
        }
        _row = row;
        BindingContext = row; // edits the row directly
    }

    private async void Save_Clicked(object sender, EventArgs e)
    {
        // If you bound Entry to double directly and it worked, you're done.
        // Otherwise parse/validate here.
        await Navigation.PopModalAsync();
    }
}