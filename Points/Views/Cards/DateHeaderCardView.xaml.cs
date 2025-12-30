using Points.Models;

namespace Points.Views.Cards;

public partial class DateHeaderCardView : ContentView
{
	public DateHeaderCardView()
	{
		InitializeComponent();

		if (BindingContext is DateHeaderCardModel model)
		{

		}
	}
}