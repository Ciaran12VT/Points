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
        if (BindingContext is not ScCardModel model)
            return;

        Action<ScCardModel> onSaved = _ => { };
        Action<ScCardModel> onDelete = _ => { };

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            // Existing card: editing should NOT add again, so onSaved can be no-op.
            // If you need to refresh totals/sorting, you can do it here (or just rely on Tick()).
            //onSaved = _ =>
            //{
            //    // e.g. vm.Tick(); or vm.SortCardsByLastActive(); etc. (only if you want)
            //};

            //onDelete = m =>
            //{
            //    // Remove from whichever page actually contains it
            //    var owner = vm.Pages.FirstOrDefault(p => p.AllCards.Contains(m));
            //    owner?.RemoveCard(m);
            //};

            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }

        //await Shell.Current.Navigation.PushAsync(new ScDetailsPage(model, onSaved, onDelete));
    }

}