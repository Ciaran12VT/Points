using Points.Helpers;
using Points.Models;
using Points.ViewModels;
using Points.Views.Details;

namespace Points.Views.Cards;

public partial class AchievementCardView : ContentView
{
	public AchievementCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not AchievementCardModel model) return;

        // Walk up to the page to get the VM
        if (this.FindParentOfType<ContentPage>()?.BindingContext is not AchievementsViewModel vm) return;

        // Collect required data
        var allTags = vm.GetAllTags();
            //vm.Pages
            //  .SelectMany(p => p.Cards)
            //  .SelectMany(c => (c.Tags ?? "")
            //      .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            //  .Distinct()
            //  .OrderBy(t => t)
            //  .ToList();

        var stepNames = vm.GetAllStepNames();

        var achievementTitles =
            vm.Pages
              .SelectMany(p => p.Cards)
              .Select(c => c.Title)
              .Distinct()
              .OrderBy(t => t)
              .ToList();

        await Shell.Current.Navigation.PushAsync(
            new AchievementDetailsPage(
                model,
                allTags,
                stepNames,
                achievementTitles,
                saved =>
                {
                    var achievementsPage = vm.Pages.First(p => p.Name == "Achievements");
                    var metaAchievementsPage = vm.Pages.First(p => p.Name == "Meta-Achievements");
                    var page = saved.GoalType == AchievementGoalType.Achievements ? metaAchievementsPage : achievementsPage;
                    vm.CommitCardToPage(page, saved, false);
                },
                deleted =>
                {
                    var achievementsPage = vm.Pages.First(p => p.Name == "Achievements");
                    var metaAchievementsPage = vm.Pages.First(p => p.Name == "Meta-Achievements");
                    var page = deleted.GoalType == AchievementGoalType.Achievements ? metaAchievementsPage : achievementsPage;
                    vm.RemoveCardFromPage(page, deleted);
                    vm.DeleteCardFromDb(deleted);
                }
            )
        );
    }

}