using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class BudgetDetailsPage : ContentPage
{
    public BudgetDetailsPage(BudgetCardModel model, Action<BudgetCardModel> onSaved, Action<BudgetCardModel> onDelete, List<string> availableTagsList)
    {
        InitializeComponent();
        BindingContext = new BudgetDetailsViewModel(model, onSaved, onDelete, availableTagsList);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is BudgetDetailsViewModel vm)
            vm.StopTimer();
    }
}