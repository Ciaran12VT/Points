using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class BudgetDetailsPage : ContentPage
{
    private readonly BudgetCardModel _model;
    private readonly IDbService _db;

    public BudgetDetailsPage(BudgetCardModel model, Action<BudgetCardModel> onSaved, Action<BudgetCardModel> onDelete, List<string> availableTagsList, IDbService db)
    {
        InitializeComponent();
        _model = model;
        _db = db;
        BindingContext = new BudgetDetailsViewModel(model, onSaved, onDelete, availableTagsList, db);
    }

    private async void OnEditUdmdClicked(object sender, EventArgs e)
    {
        if (_model.CardID <= 0)
        {
            await DisplayAlert("Save required", "Please save the card before configuring metadata fields.", "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new UdmdConfigPage(_model.CardID, _db));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is BudgetDetailsViewModel vm)
            vm.StopTimer();
    }
}
