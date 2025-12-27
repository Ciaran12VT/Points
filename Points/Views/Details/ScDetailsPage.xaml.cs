using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class ScDetailsPage : ContentPage
{
    public ScDetailsPage(ScCardModel model, Action<ScCardModel> onSaved, Action<ScCardModel> onDelete)
    {
        InitializeComponent();
        BindingContext = new ScDetailsViewModel(model, onSaved, onDelete);
    }

}