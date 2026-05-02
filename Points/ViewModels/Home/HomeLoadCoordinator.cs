using Points.Global;
using Points.Models;
using Points.Services;
using Points.Services.Scheduling;
using Points.Services.Persistence;
using Points.Services.Time;

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
        private readonly Func<int> _getPosition;
        private readonly Action<int> _setPosition;
        private readonly Action _setSelectedPageIcon;
        private readonly Action _sortMissionCards;
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
            Func<int> getPosition,
            Action<int> setPosition,
            Action setSelectedPageIcon,
            Action sortMissionCards,
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
            _getPosition = getPosition;
            _setPosition = setPosition;
            _setSelectedPageIcon = setSelectedPageIcon;
            _sortMissionCards = sortMissionCards;
            _notifyPropertyChanged = notifyPropertyChanged;
        }

        public async Task LoadAsync()
        {
            var settings = await _settings.GetSettingsAsync();
            SettingsProvider.Initialize(settings);

            var pageToRestore = GetPageToRestore();

            _pageState.InitializePages(settings);

            var now = _clock.LocalNow;
            var seedRangeStart = MinDateTime(GlobalVariables.RangeStart, new TimeScopeRange(TimeScope.Daily, now).Start);
            var seedRangeEnd = MaxDateTime(GlobalVariables.RangeEnd, new TimeScopeRange(TimeScope.Monthly, now).End);
            var seed = await _cardReader.GetHomeSeedDataAsync(seedRangeStart, seedRangeEnd);
            var goalProgressCards = await _goalsPage.BuildGoalProgressCardsAsync(seed.MainQuestCards);
            var openActivity = await _activity.GetCurrentActiveActivityAsync();
            var shortcuts = await _dashboardShortcuts.GetDashboardShortcutsAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HydratePages(seed, shortcuts, openActivity, goalProgressCards);
            });

            await _scheduleCoordinator.SyncEnabledSchedulesAsync();

            RestorePosition(pageToRestore);
        }

        private string GetPageToRestore()
        {
            var position = _getPosition();

            return _pages.Count > 0 && position >= 0 && position < _pages.Count
                ? _pages[position].Name
                : "Dashboard";
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

            CommitCards(mainQuest, seed.MainQuestCards);
            CommitCards(mission, seed.MissionCards);
            CommitCards(budgets, seed.BudgetCards);
            CommitCards(
                achievements,
                seed.Achievements
                    .Cast<AchievementCardModel>()
                    .Where(x => x.IsPinned)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.CardID));
            CommitCards(
                trackers,
                seed.ValueTrackers
                    .Concat(seed.EventTrackers)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.CardID));

            _activityInteraction.RestoreActiveCardFromOpenActivity(openActivity);

            var now = _clock.LocalNow;
            _runtimeTicks.RefreshBudgetCards(now);
            _sortMissionCards();
            _notifyPropertyChanged(nameof(HomeViewModel.HasNegativeAvailableMission));
            _notifyPropertyChanged(nameof(HomeViewModel.GlobalValueColor));
            _notifyPropertyChanged(nameof(HomeViewModel.ActivePhaseName));
            _notifyPropertyChanged(nameof(HomeViewModel.ActivePhaseColor));

            _goalsPage.AppendGoalProgressCards(goals, goalProgressCards);
        }

        private void CommitCards(HomePageModel? page, IEnumerable<ICardModel> cards)
        {
            if (page == null)
                return;

            foreach (var card in cards)
                _cardLifecycle.CommitCardToPage(page, card, true);
        }

        private void RestorePosition(string? pageName)
        {
            if (_pages.Count == 0)
                return;

            var page = !string.IsNullOrWhiteSpace(pageName)
                ? _pages.FirstOrDefault(p => p.Name == pageName)
                : null;

            page ??= _pages.FirstOrDefault(p => p.Name == "Main Quest") ?? _pages.First();

            _setPosition(IndexOfPage(page));
            _setSelectedPageIcon();
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
