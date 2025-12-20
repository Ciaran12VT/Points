using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class ScDetailsPage : ContentPage
{
    public ScDetailsPage(ScCardModel model, DateTime rangeStart, DateTime rangeEnd)
    {
        InitializeComponent();
        BindingContext = new ScDetailsViewModel(model, rangeStart, rangeEnd);
    }
}