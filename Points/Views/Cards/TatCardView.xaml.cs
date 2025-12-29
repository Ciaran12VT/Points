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
        if (BindingContext is not TatCardModel model)
            return;

        Action<TatCardModel> onSaved = _ => { };
        Action<TatCardModel> onDelete = _ => { };

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            //onSaved = _ =>
            //{
            //    // no-op for existing; optionally refresh totals/sorting if needed
            //};

            //onDelete = m =>
            //{
            //    var owner = vm.Pages.FirstOrDefault(p => p.AllCards.Contains(m));
            //    owner?.RemoveCard(m);
            //};

            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }

        //await Shell.Current.Navigation.PushAsync(new TatDetailsPage(model, onSaved, onDelete));
    }

}