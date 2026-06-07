using Points.Services.Navigation;
using Points.ViewModels.Goals;

namespace Points.Views.Goals;

public partial class EditGoalPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

    public Command DoneCommand { get; }

    public EditGoalPage(
        GoalProgressRowVm row,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
	{
        DoneCommand = new Command(async () => await SaveAsync());
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            MainThread.BeginInvokeOnMainThread(async () =>
                await _dialogs.DisplayAlertAsync("XAML load failed", ex.ToString(), "OK"));
            throw;
        }
        BindingContext = row; // edits the row directly
    }

    private async Task SaveAsync()
    {
        // If you bound Entry to double directly and it worked, you're done.
        // Otherwise parse/validate here.
        await _navigation.PopModalAsync();
    }
}
