using Points.Models;
using Points.Services.Navigation;
using Points.Services.Time;
using Points.ViewModels.Achievements;

namespace Points.Views.Achievements;

public partial class AchievementDetailsPage : ContentPage
{
    public AchievementDetailsPage(
        AchievementCardModel model,
        IEnumerable<string> allTags,
        IEnumerable<string> stepNames,
        IEnumerable<string> achievementTitles,
        Func<AchievementCardModel, Task> onSaved,
        Action<AchievementCardModel> onDelete,
        IClock clock,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        BindingContext = new AchievementDetailsViewModel(
            model,
            allTags,
            stepNames,
            achievementTitles,
            onSaved,
            onDelete,
            navigation,
            dialogs,
            clock);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is AchievementDetailsViewModel vm)
            vm.StopTimer();
    }
}
