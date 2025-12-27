using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class TatDetailsPage : ContentPage
{
    public TatDetailsPage(TatCardModel model, Action<TatCardModel> onSaved, Action<TatCardModel> onDelete)
    {
        InitializeComponent();
        BindingContext = new TatDetailsViewModel(model, onSaved, onDelete);
    }

}