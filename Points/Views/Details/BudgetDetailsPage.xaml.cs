using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class BudgetDetailsPage : ContentPage
{
    private readonly BudgetCardModel _model;
    private readonly IUdmdService _udmd;

    public BudgetDetailsPage(BudgetCardModel model, Action<BudgetCardModel> onSaved, Action<BudgetCardModel> onDelete, List<string> availableTagsList, IUdmdService udmd)
    {
        InitializeComponent();
        _model = model;
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        BindingContext = new BudgetDetailsViewModel(model, onSaved, onDelete, availableTagsList, udmd);
    }

    private async void OnEditUdmdClicked(object sender, EventArgs e)
    {
        if (_model.CardID <= 0)
        {
            await DisplayAlert("Save required", "Please save the card before configuring metadata fields.", "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new UdmdConfigPage(_model.CardID, _udmd));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is BudgetDetailsViewModel vm)
            vm.StopTimer();
    }
}
