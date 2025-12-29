using CommunityToolkit.Maui.Behaviors;
using Points.Helpers;
using Points.Models;
using Points.ViewModels;
using Points.Views.Details;

namespace Points.Views.Cards;

public partial class MissionCardView : ContentView
{
    private TouchBehavior? _touch;

    public MissionCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not MissionCardModel model)
            return;

        // For existing cards, Save should NOT add a new card.
        // We'll use the callback to request a refresh/sort if desired.
        Action<MissionCardModel> onSaved = _ => { };
        Action<MissionCardModel> onDelete = _ => { };
        Action<MissionCardModel> onFail = _ => { };

        // If you want to re-sort missions after editing (recommended):
        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            //onSaved = _ =>
            //{
            //    // If you already have a method that sorts mission cards, call it here.
            //    // vm.SortMissionCards();
            //    // Otherwise, no-op is fine.
            //};
            //onDelete = vm.DeleteMission;
            //onFail = vm.FailMission;

            await vm.OpenExistingCardAsync((ICardModel)BindingContext);
        }

        //await Shell.Current.Navigation.PushAsync(new MissionDetailsPage(model, onSaved, onDelete, onFail));
    }
}