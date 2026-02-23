using Points.Helpers;
using Points.Models;
using Points.Services.Locks;
using Points.ViewModels;
using Points.Views.Details;

namespace Points.Views.Cards;

public partial class ScCardView : ContentView
{
	public ScCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not ScCardModel model)
            return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }
    }


    private async void OnAddRepClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ScCardModel model) return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is not HomeViewModel vm) return;

        var now = (Shell.Current?.CurrentPage?.BindingContext as dynamic)?.Now as DateTime? ?? DateTime.Now;

        if (LockEvaluator.IsLockedNow(model, now, vm.GetActiveCardModels(), out var availableAt))
        {
            var rem = LockEvaluator.FormatRemaining(now, availableAt);
            await Shell.Current.DisplayAlert("Locked", $"This card is locked. Available in {rem}.", "OK");
            return;
        }

        if (model.Steps[0].IncrementCommand.CanExecute(null))
        {
            model.Steps[0].IncrementCommand.Execute(null);
            await Task.Yield();
            await vm.IncrementFirstStep(model);
        }
    }
}