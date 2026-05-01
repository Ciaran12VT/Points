using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Budgets;

namespace Points.Views.Budgets;

public partial class BudgetDetailsPage : ContentPage
{
    public BudgetDetailsPage(
        BudgetCardModel model,
        Action<BudgetCardModel> onSaved,
        Action<BudgetCardModel> onDelete,
        List<string> availableTagsList,
        IUdmdService udmd,
        IClock clock,
        ITimeZoneService timeZoneService,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        BindingContext = new BudgetDetailsViewModel(
            model,
            onSaved,
            onDelete,
            availableTagsList,
            udmd,
            navigation,
            dialogs,
            clock,
            timeZoneService);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is BudgetDetailsViewModel vm)
            vm.StopTimer();
    }
}
