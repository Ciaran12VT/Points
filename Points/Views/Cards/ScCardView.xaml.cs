using Points.Helpers;
using Points.Models;
using Points.ViewModels;
using Points.Views.Details;

namespace Points.Views.Cards;

public partial class ScCardView : ContentView
{
	public ScCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is ScCardModel model)
        {
            var page = this.FindParentOfType<ContentPage>();
            if (page?.BindingContext is HomeViewModel vm)
                await Shell.Current.Navigation.PushAsync(new ScDetailsPage(model));
            else
                await Shell.Current.Navigation.PushAsync(new ScDetailsPage(model));
        }
    }
}