using CommunityToolkit.Maui.Behaviors;
using Points.Helpers;
using Points.Models;
using Points.Services.Locks;
using Points.ViewModels;
using Points.Views.Details;

namespace Points.Views.Cards;

public partial class MissionCardView : ContentView
{
    private TouchBehavior? _touch;

    public MissionCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not MissionCardModel model)
            return;

        //Prompt the user to confirm if the want to mark this mission as complete

        // For existing cards, Save should NOT add a new card.
        // We'll use the callback to request a refresh/sort if desired.
        Action<MissionCardModel> onSaved = _ => { };
        Action<MissionCardModel> onDelete = _ => { };
        Action<MissionCardModel> onFail = _ => { };

        // If you want to re-sort missions after editing (recommended):
        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }
    }

    private async void OnCompleteClicked(object sender, EventArgs e)
    {
        if (BindingContext is not MissionCardModel model)
            return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            var now = (Shell.Current?.CurrentPage?.BindingContext as HomeViewModel)?.Now ?? DateTime.Now;

            if (LockEvaluator.IsLockedNow(model, now, vm.GetActiveCardModels(), out var availableAt))
            {
                var rem = LockEvaluator.FormatRemaining(now, availableAt);
                await Shell.Current.DisplayAlert("Locked", $"This mission is locked. Available in {rem}.", "OK");
                return;
            }

            if (!model.IsComplete)
            {
                bool confirm = await page.DisplayAlert(
                "Complete mission?",
                    $"Mark as complete?",
                    "Complete",
                    "Cancel");

                if (confirm)
                {
                    // Option A: if the model exposes a CompleteCommand (like your XAML implies)
                    if (model.CompleteCommand?.CanExecute(null) == true)
                        model.CompleteCommand.Execute(null);

                    await Task.Yield();
                    await vm.SaveMission(model);
                    
                }
            }
        }
    }
}