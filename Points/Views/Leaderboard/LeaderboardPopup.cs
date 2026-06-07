using CommunityToolkit.Maui.Views;
using Points.Services.Navigation;
using Points.ViewModels.Leaderboard;

namespace Points.Views.Leaderboard;

public sealed class LeaderboardPopup : Popup
{
    private readonly LeaderboardViewModel _viewModel;
    private readonly LeaderboardContentRenderer _leaderboardRenderer;
    private readonly LeaderboardPlannerInteractionCoordinator _plannerInteractions;
    private readonly LeaderboardPlannerContentRenderer _plannerRenderer;

    public LeaderboardPopup(
        LeaderboardViewModel viewModel,
        IAppDialogService dialogs)
    {
        _viewModel = viewModel;
        _leaderboardRenderer = new LeaderboardContentRenderer(_viewModel);
        _plannerInteractions = new LeaderboardPlannerInteractionCoordinator(
            _viewModel,
            dialogs ?? throw new ArgumentNullException(nameof(dialogs)));
        _plannerRenderer = new LeaderboardPlannerContentRenderer(_viewModel, _plannerInteractions);

        BindingContext = _viewModel;
        CanBeDismissedByTappingOutsideOfPopup = true;

        Content = LeaderboardPopupChrome.BuildFrame(
            LeaderboardPopupChrome.GetPopupSize(),
            BuildRoot());

        MainThread.BeginInvokeOnMainThread(async () => await _viewModel.RefreshAsync());
    }

    private View BuildRoot()
    {
        var root = LeaderboardPopupChrome.CreateRoot();

        root.Add(LeaderboardPopupChrome.BuildHeader(() => Close()), 0, 0);
        root.Add(LeaderboardPopupChrome.BuildTabs(), 0, 1);
        root.Add(BuildContent(), 0, 2);

        return root;
    }

    private View BuildContent()
    {
        var content = new Grid();

        var leaderboard = _leaderboardRenderer.Build();
        leaderboard.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.IsLeaderboardSelected));

        var planner = _plannerRenderer.Build();
        planner.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.IsPlannerSelected));

        content.Add(leaderboard);
        content.Add(planner);

        return content;
    }
}
