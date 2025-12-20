using Points.Helpers;
using Points.Models;
using Points.Views.Details;

namespace Points.Views.Cards;

public partial class TatCardView : ContentView
{
	public TatCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is TatCardModel model)
        {
            // pull the current window from HomeViewModel via the parent page BindingContext
            if (this.FindParentOfType<ContentPage>()?.BindingContext is Points.ViewModels.HomeViewModel vm)
            {
                await Shell.Current.Navigation.PushAsync(new TatDetailsPage(model, vm.RangeStart, vm.RangeEnd));
            }
            else
            {
                await Shell.Current.Navigation.PushAsync(new TatDetailsPage(model, DateTime.Today, DateTime.Now));
            }
        }
    }
}