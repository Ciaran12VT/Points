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
    }


    private async void OnCardTapped(object sender, EventArgs e)
    {
        if (BindingContext is not Points.Models.BudgetCardModel model)
            return;

        await Shell.Current.Navigation.PushAsync(
            new Points.Views.Details.BudgetDetailsPage(model, _ => { })
        );
    }

}