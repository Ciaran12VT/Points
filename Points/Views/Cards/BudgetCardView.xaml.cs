using Points.Helpers;
using Points.Models;
using Points.Services;
using Points.ViewModels;
using Points.Views.Details;

namespace Points.Views.Cards;

public partial class BudgetCardView : ContentView
{
    public BudgetCardView()
	{
		InitializeComponent();
    }
    private async void OnSpendClicked(object sender, EventArgs e)
    {
        if (BindingContext is not Points.Models.BudgetCardModel b)
            return;

        var input = await Shell.Current.DisplayPromptAsync(
            "Spend",
            $"How many {b.Currency} do you want to spend?",
            accept: "OK",
            cancel: "Cancel",
            placeholder: "e.g. 250",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (!double.TryParse(input, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            await Shell.Current.DisplayAlert("Invalid number", "Please enter a valid amount.", "OK");
            return;
        }

        if (amount <= 0)
            return;

        b.AddSpend(amount);

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.SaveBudget(b);
        }
    }

    private async void OnCashInClicked(object sender, EventArgs e)
    {
        if (BindingContext is not Points.Models.BudgetCardModel b)
            return;

        var input = await Shell.Current.DisplayPromptAsync(
            "Cash In",
            $"How many {b.Currency} do you want to cash in?",
            accept: "OK",
            cancel: "Cancel",
            placeholder: "e.g. 100",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (!double.TryParse(input, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            await Shell.Current.DisplayAlert("Invalid number", "Please enter a valid amount.", "OK");
            return;
        }

        if (amount <= 0)
            return;

        b.AddCashIn(amount);

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.SaveBudget(b);
        }
    }


    private async void OnCardTapped(object sender, EventArgs e)
    {
        if (BindingContext is not Points.Models.BudgetCardModel model)
            return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            //// Existing card: editing should NOT add again, so onSaved can be no-op.
            //// If you need to refresh totals/sorting, you can do it here (or just rely on Tick()).
            //onSaved = _ =>
            //{
            //    // e.g. vm.Tick(); or vm.SortCardsByLastActive(); etc. (only if you want)
            //};

            //onDelete = m =>
            //{
            //    // Remove from whichever page actually contains it
            //    var owner = vm.Pages.FirstOrDefault(p => p.AllCards.Contains(m));
            //    owner?.RemoveCard(m);
            //};

            await vm.OpenExistingCardAsync((ICardModel)BindingContext);
        }

        //await Shell.Current.Navigation.PushAsync(new BudgetDetailsPage(model, onSaved, onDelete));
    }

}