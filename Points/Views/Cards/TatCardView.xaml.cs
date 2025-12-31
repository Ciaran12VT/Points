using Points.Global;
using Points.Helpers;
using Points.Models;
using Points.ViewModels;
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
        if (BindingContext is not TatCardModel model) return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }
    }

}