using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class ScDetailsPage : ContentPage
{
    public ScDetailsPage(ScCardModel model)
    {
        InitializeComponent();
        BindingContext = new ScDetailsViewModel(model);
    }

    public ScDetailsPage(ScCardModel model, Action<ScCardModel> onSaved)
    {
        InitializeComponent();
        BindingContext = new ScDetailsViewModel(model, onSaved);
    }

}