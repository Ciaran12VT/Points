using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class MissionDetailsPage : ContentPage
{
    public MissionDetailsPage(MissionCardModel model, Action<MissionCardModel> onSaved)
    {
        InitializeComponent();
        BindingContext = new MissionDetailsViewModel(model, onSaved);
    }
}