using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class TatDetailsPage : ContentPage
{
    public TatDetailsPage(TatCardModel model, DateTime rangeStart, DateTime rangeEnd)
    {
        InitializeComponent();
        BindingContext = new TatDetailsViewModel(model, rangeStart, rangeEnd);
    }
}