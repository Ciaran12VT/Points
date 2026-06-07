using Points.Global;
using Points.Helpers;
using Points.Models;
using Points.Services.Time;
using Points.ViewModels.Home;

namespace Points.Views.Cards;

public partial class BudgetCardView : ContentView
{
    private static readonly IClock FallbackClock = new SystemClock();

    public BudgetCardView()
	{
		InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is BudgetCardModel budget)
        {
            budget.IsCashInEnabled = SettingsProvider.IsCashInEnabled;
            budget.NotifyTimeChanged(GetCurrentTime());
        }
    }

    private DateTime GetCurrentTime()
    {
        var page = this.FindParentOfType<ContentPage>();
        return page?.BindingContext is HomeViewModel vm ? vm.Now : FallbackClock.LocalNow;
    }


}
