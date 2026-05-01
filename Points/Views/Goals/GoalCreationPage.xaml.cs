using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Goals;

namespace Points.Views.Goals;

public partial class GoalCreationPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

	public GoalCreationPage(
        ICardReadService cardReader,
        IGoalService goals,
        IClock clock,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
	{
		InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

		BindingContext = new GoalCreationViewModel(
            cardReader,
            goals,
            _navigation,
            clock);

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
                var page = new EditGoalPage(selected, _navigation, _dialogs);
                await _navigation.PushModalAsync(new NavigationPage(page));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await _dialogs.DisplayAlertAsync("Crash during navigation", ex.ToString(), "OK");
        }
    }


}
