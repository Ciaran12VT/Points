using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class MissionDetailsPage : ContentPage
{
    public MissionDetailsPage(
        MissionCardModel model,
        Action<MissionCardModel> onSaved,
        Action<MissionCardModel> onDelete,
        Action<MissionCardModel> onFail)
    {
        InitializeComponent();
        BindingContext = new MissionDetailsViewModel(model, onSaved, onDelete, onFail);
    }
}