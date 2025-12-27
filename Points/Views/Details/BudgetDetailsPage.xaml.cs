using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class BudgetDetailsPage : ContentPage
{
    public BudgetDetailsPage(BudgetCardModel model, Action<BudgetCardModel> onSaved, Action<BudgetCardModel> onDelete)
    {
        InitializeComponent();
        BindingContext = new BudgetDetailsViewModel(model, onSaved, onDelete);
    }
}