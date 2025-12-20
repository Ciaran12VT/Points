namespace Points.Views.Cards;

public partial class BudgetCardView : ContentView
{
	public BudgetCardView()
	{
		InitializeComponent();
	}
    private async void OnSpendClicked(object sender, EventArgs e)
    {
        if (BindingContext is Points.Models.BudgetCardModel b)
        {
            // temporary: subtract 50 units
            b.AddSpend(50);
        }
        await Task.CompletedTask;
    }

    private async void OnCashInClicked(object sender, EventArgs e)
    {
        if (BindingContext is Points.Models.BudgetCardModel b)
        {
            // temporary: cash in 100 units
            b.AddCashIn(100);
        }
        await Task.CompletedTask;
    }
}