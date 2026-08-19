using Points.Global;
using Points.Models;
using Points.Services;
using Points.Services.Scheduling;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Services.Diagnostics;

namespace Points.ViewModels.Home
{
    internal sealed class HomeLoadCoordinator
    {
        private readonly IReadOnlyList<HomePageModel> _pages;
        private readonly ISettingsService _settings;
        private readonly ICardReadService _cardReader;
        private readonly IActivityService _activity;
        private readonly INotificationScheduleCoordinator _scheduleCoordinator;
        private readonly IClock _clock;
        private readonly HomePageStateCoordinator _pageState;
        private readonly HomeActivityInteractionCoordinator _activityInteraction;
        private readonly HomeCardLifecycleCoordinator _cardLifecycle;
        private readonly HomeDashboardShortcutWorkflowCoordinator _dashboardShortcuts;
        private readonly HomeGoalsPageCoordinator _goalsPage;
        private readonly HomeRuntimeTickCoordinator _runtimeTicks;
        private readonly IUserMultiplierService _userMultipliers;
        private readonly Func<HomePageModel?> _getSelectedPage;
        private readonly Func<int> _getPosition;
        private readonly Action _beginPageReconciliation;
        private readonly Action<HomePageModel?, bool> _completePageReconciliation;
        private readonly Action<string> _notifyPropertyChanged;

        public HomeLoadCoordinator(
            IReadOnlyList<HomePageModel> pages,
            ISettingsService settings,
            ICardReadService cardReader,
            IActivityService activity,
            INotificationScheduleCoordinator scheduleCoordinator,
            IClock clock,
            HomePageStateCoordinator pageState,
            HomeActivityInteractionCoordinator activityInteraction,
            HomeCardLifecycleCoordinator cardLifecycle,
            HomeDashboardShortcutWorkflowCoordinator dashboardShortcuts,
            HomeGoalsPageCoordinator goalsPage,
            HomeRuntimeTickCoordinator runtimeTicks,
            IUserMultiplierService userMultipliers,
            Func<HomePageModel?> getSelectedPage,
            Func<int> getPosition,
            Action beginPageReconciliation,
            Action<HomePageModel?, bool> completePageReconciliation,
            Action<string> notifyPropertyChanged)
        {
            _pages = pages;
            _settings = settings;
            _cardReader = cardReader;
            _activity = activity;
            _scheduleCoordinator = scheduleCoordinator;
            _clock = clock;
            _pageState = pageState;
            _activityInteraction = activityInteraction;
            _cardLifecycle = cardLifecycle;
            _dashboardShortcuts = dashboardShortcuts;
            _goalsPage = goalsPage;
            _runtimeTicks = runtimeTicks;
            _userMultipliers = userMultipliers ?? throw new ArgumentNullException(nameof(userMultipliers));
            _getSelectedPage = getSelectedPage;
            _getPosition = getPosition;
            _beginPageReconciliation = beginPageReconciliation;
            _completePageReconciliation = completePageReconciliation;
            _notifyPropertyChanged = notifyPropertyChanged;
        }

        public async Task<bool> LoadAsync(
            DateTime rangeStart,
            DateTime rangeEnd,
            Func<Action, bool> tryCommit,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tryCommit);

            var settings = await _settings.GetSettingsAsync();
            await _userMultipliers.GetActiveMultiplierAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var now = _clock.LocalNow;
            var seedRangeStart = MinDateTime(rangeStart, new TimeScopeRange(TimeScope.Daily, now).Start);
            var seedRangeEnd = MaxDateTime(rangeEnd, new TimeScopeRange(TimeScope.Monthly, now).End);
            var seed = await _cardReader.GetHomeSeedDataAsync(seedRangeStart, seedRangeEnd);
            var goalProgressCards = await _goalsPage.BuildGoalProgressCardsAsync(seed.MainQuestCards);
            var openActivity = await _activity.GetCurrentActiveActivityAsync();
            var shortcuts = await _dashboardShortcuts.GetDashboardShortcutsAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var committed = false;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                committed = tryCommit(() =>
                {
                    SettingsProvider.Initialize(settings);
                    var selectedPage = _getSelectedPage();
                    var selectedIndex = selectedPage == null ? _getPosition() : IndexOfPage(selectedPage);
                    _beginPageReconciliation();
                    var layoutChanged = false;

                    try
                    {
                        var reconciliation = _pageState.ReconcilePages(
                            settings,
                            selectedPage,
                            selectedIndex);
                        layoutChanged = reconciliation.LayoutChanged;

                        HydratePages(seed, shortcuts, openActivity, goalProgressCards);
                        _completePageReconciliation(
                            reconciliation.SelectedPage,
                            reconciliation.LayoutChanged);
                    }
                    catch
                    {
                        var fallback = _pageState.ResolveSelectedPage(selectedPage, selectedIndex);
                        _completePageReconciliation(fallback, layoutChanged);
                        throw;
                    }
                });
            });

            if (!committed)
                return false;

            TaskSupervisor.Forget(
                _scheduleCoordinator.SyncEnabledSchedulesAsync(),
                "Synchronize schedules after Home refresh");
            return true;
        }

        private void HydratePages(
            HomeSeedData seed,
            IEnumerable<ShortcutModel> shortcuts,
            ActivityModel? openActivity,
            IReadOnlyList<ICardModel> goalProgressCards)
        {
            var dashboard = _pages.FirstOrDefault(p => p.Name == "Dashboard");
            var mainQuest = _pages.FirstOrDefault(p => p.Name == "Main Quest");
            var mission = _pages.FirstOrDefault(p => p.Name == "Mission");
            var budgets = _pages.FirstOrDefault(p => p.Name == "Budgets");
            var achievements = _pages.FirstOrDefault(p => p.Name == "Challenges & Pinned Achievements");
            var trackers = _pages.FirstOrDefault(p => p.Name == "Arcs");
            var goals = _pages.FirstOrDefault(p => p.Name == "Goals");

            if (dashboard != null)
                _dashboardShortcuts.RebuildDashboardCells(shortcuts);

            ReplaceCards(mainQuest, seed.MainQuestCards);
            ReplaceCards(mission, seed.MissionCards);
            ReplaceCards(budgets, seed.BudgetCards);
            ReplaceCards(
                achievements,
                seed.Achievements
                    .Cast<AchievementCardModel>()
                    .Where(x => x.IsPinned)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.CardID));
            ReplaceCards(
                trackers,
                seed.ValueTrackers
                    .Concat(seed.EventTrackers)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.CardID));

            _activityInteraction.RestoreActiveCardFromOpenActivity(openActivity);

            var now = _clock.LocalNow;
            _runtimeTicks.RefreshBudgetCards(now);
            _notifyPropertyChanged(nameof(HomeViewModel.HasNegativeAvailableMission));
            _notifyPropertyChanged(nameof(HomeViewModel.GlobalValueColor));
            _notifyPropertyChanged(nameof(HomeViewModel.ActivePhaseName));
            _notifyPropertyChanged(nameof(HomeViewModel.ActivePhaseColor));
            _notifyPropertyChanged(nameof(HomeViewModel.ActiveMultiplierCode));
            _notifyPropertyChanged(nameof(HomeViewModel.HasActiveMultiplier));

            _goalsPage.ReplaceGoalProgressCards(goals, goalProgressCards);
        }

        private void ReplaceCards(HomePageModel? page, IEnumerable<ICardModel> cards)
        {
            _cardLifecycle.ReplaceCardsForLoad(page, cards);
        }

        private int IndexOfPage(HomePageModel page)
        {
            for (var i = 0; i < _pages.Count; i++)
            {
                if (ReferenceEquals(_pages[i], page))
                    return i;
            }

            return -1;
        }

        private static DateTime MinDateTime(DateTime left, DateTime right)
        {
            return left <= right ? left : right;
        }

        private static DateTime MaxDateTime(DateTime left, DateTime right)
        {
            return left >= right ? left : right;
        }
    }
}
