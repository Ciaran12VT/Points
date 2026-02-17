using Points.Models;
using Points.ViewModels;

namespace Points.Views.Planners;

public partial class PlannerCreationPage : ContentPage
{
	public PlannerCreationPage(List<IActiveCardModel> cards)
	{
		InitializeComponent();

		BindingContext = new PlannerCreationViewModel(cards);

    }
}