using System.Collections.ObjectModel;
using Points.Global;
using Points.Models;
using Points.Services.Time;

namespace Points.ViewModels.Home
{
    internal sealed class HomePageStateCoordinator
    {
        private static readonly IReadOnlyList<PageDefinition> PageDefinitions =
        [
            new(
                title: "Dashboard",
                icon: "𓃑",
                defaultOrder: 1,
                activeSettingKey: SettingKeys.DashboardActive,
                orderSettingKey: SettingKeys.DashboardScreenOrder),

            new(
                title: "Main Quest",
                icon: "♻",
                defaultOrder: 2,
                activeSettingKey: SettingKeys.MainQuestActive,
                orderSettingKey: SettingKeys.MainQuestScreenOrder),

            new(
                title: "Mission",
                icon: "⚑",
                defaultOrder: 3,
                activeSettingKey: SettingKeys.MissionActive,
                orderSettingKey: SettingKeys.MissionScreenOrder),

            new(
                title: "Budgets",
                icon: "◷",
                defaultOrder: 4,
                activeSettingKey: SettingKeys.BudgetsActive,
                orderSettingKey: SettingKeys.BudgetsScreenOrder),

            new(
                title: "Challenges & Pinned Achievements",
                icon: "★",
                defaultOrder: 5,
                activeSettingKey: SettingKeys.AchievementsActive,
                orderSettingKey: SettingKeys.AchievementsScreenOrder),

            new(
                title: "Arcs",
                icon: "∿",
                defaultOrder: 6,
                activeSettingKey: SettingKeys.ArcsActive,
                orderSettingKey: SettingKeys.ArcsScreenOrder),

            new(
                title: "Goals",
                icon: "☰",
                defaultOrder: 7,
                activeSettingKey: SettingKeys.GoalsActive,
                orderSettingKey: SettingKeys.GoalsScreenOrder)
        ];

        private readonly ObservableCollection<HomePageModel> _pages;
        private readonly IClock _clock;
        private readonly IReadOnlyDictionary<string, HomePageModel> _canonicalPages;
        private MainQuestFilterMode _mainQuestFilterMode = MainQuestFilterMode.None;

        public HomePageStateCoordinator(ObservableCollection<HomePageModel> pages, IClock clock)
        {
            _pages = pages;
            _clock = clock;
            _canonicalPages = PageDefinitions.ToDictionary(
                definition => definition.Title,
                definition =>
                    _pages.FirstOrDefault(page => page.Name == definition.Title)
                    ?? CreatePage(definition),
                StringComparer.Ordinal);
        }

        public HomePageReconciliationResult ReconcilePages(
            List<AcquiredSetting> settings,
            HomePageModel? previouslySelectedPage,
            int previousPosition)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _mainQuestFilterMode = MainQuestFilterMode.None;

            var desiredPages = PageDefinitions
                .Select(def => new
                {
                    Definition = def,
                    IsActive = GetBoolSetting(settings, def.ActiveSettingKey, true),
                    ScreenOrder = GetIntSetting(settings, def.OrderSettingKey, def.DefaultOrder)
                })
                .Where(x => x.IsActive)
                .OrderBy(x => x.ScreenOrder)
                .ThenBy(x => x.Definition.DefaultOrder)
                .Select(x => _canonicalPages[x.Definition.Title])
                .ToList();

            var layoutChanged = false;

            for (var desiredIndex = 0; desiredIndex < desiredPages.Count; desiredIndex++)
            {
                var desiredPage = desiredPages[desiredIndex];
                if (desiredIndex < _pages.Count && ReferenceEquals(_pages[desiredIndex], desiredPage))
                    continue;

                var existingIndex = IndexOfReference(_pages, desiredPage);
                if (existingIndex >= 0)
                    _pages.Move(existingIndex, desiredIndex);
                else
                    _pages.Insert(desiredIndex, desiredPage);

                layoutChanged = true;
            }

            while (_pages.Count > desiredPages.Count)
            {
                var removedPage = _pages[^1];
                _pages.RemoveAt(_pages.Count - 1);
                removedPage.ReplaceCards(Array.Empty<ICardModel>());
                removedPage.DashboardCells.Clear();
                layoutChanged = true;
            }

            var selectedPage = ResolveSelectedPage(previouslySelectedPage, previousPosition);
            var selectedIndex = selectedPage == null ? -1 : IndexOfReference(_pages, selectedPage);

            return new HomePageReconciliationResult(layoutChanged, selectedPage, selectedIndex);
        }

        public HomePageModel? ResolveSelectedPage(
            HomePageModel? previouslySelectedPage,
            int previousPosition)
        {
            if (previouslySelectedPage != null && IndexOfReference(_pages, previouslySelectedPage) >= 0)
                return previouslySelectedPage;

            if (_pages.Count == 0)
                return null;

            if (previouslySelectedPage != null)
                return _pages[Math.Clamp(previousPosition, 0, _pages.Count - 1)];

            return _pages.FirstOrDefault(page => page.Name == "Dashboard")
                ?? _pages.FirstOrDefault(page => page.Name == "Main Quest")
                ?? _pages[0];
        }

        public List<IActiveCardModel> GetActiveCardModels()
        {
            var mainQuest = _pages.FirstOrDefault(p => p.Name == "Main Quest");
            var mission = _pages.FirstOrDefault(p => p.Name == "Mission");

            var merge = new List<IActiveCardModel>();

            if (mainQuest != null)
                merge.AddRange(mainQuest.AllCards.OfType<IActiveCardModel>());

            if (mission != null)
                merge.AddRange(mission.AllCards.OfType<IActiveCardModel>());

            return merge;
        }

        public bool HasNegativeAvailableMission()
        {
            var now = _clock.LocalNow;

            var missionPage = _pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null)
                return false;

            foreach (var mission in missionPage.AllCards.OfType<MissionCardModel>())
            {
                if (mission.IsComplete)
                    continue;

                if (now < mission.AvailableFromDate)
                    continue;

                if (mission.GetCurrentValue(now) < 0)
                    return true;
            }

            return false;
        }

        public void ApplyPositiveFilter(HomePageModel page)
        {
            if (page.Name != "Main Quest")
                return;

            _mainQuestFilterMode =
                _mainQuestFilterMode == MainQuestFilterMode.PositiveOnly
                    ? MainQuestFilterMode.None
                    : MainQuestFilterMode.PositiveOnly;

            ApplyMainQuestFilter(page);
        }

        public void ApplyNegativeFilter(HomePageModel page)
        {
            if (page.Name != "Main Quest")
                return;

            _mainQuestFilterMode =
                _mainQuestFilterMode == MainQuestFilterMode.NegativeOnly
                    ? MainQuestFilterMode.None
                    : MainQuestFilterMode.NegativeOnly;

            ApplyMainQuestFilter(page);
        }

        public void ClearFilters(HomePageModel page)
        {
            if (page.Name != "Main Quest" && page.Name != "Mission")
                return;

            page.ResetVisible();
            SortMissionCards();
        }

        public void SortCardsByLastActive(HomePageModel page)
        {
            if (page.Name != "Main Quest")
                return;

            page.SortCardsByLastActive();
        }

        public void FilterCardsByTag(string choice)
        {
            if (string.IsNullOrEmpty(choice))
                return;

            foreach (var page in _pages)
                page.FilterCardsByTag(choice);
        }

        public void FilterCardsBySearchTerm(string input)
        {
            if (string.IsNullOrEmpty(input))
                return;

            foreach (var page in _pages)
                page.FilterCardsBySearchTerm(input);
        }

        public List<string> GetTags()
        {
            var result = new List<string>();

            foreach (var page in _pages)
            {
                foreach (var card in page.AllCards)
                {
                    var cardTags = card.Tags.Replace('#', ' ').Replace(',', ' ')
                        .Split(' ')
                        .Select(x => x.Trim());

                    foreach (var cardTag in cardTags)
                    {
                        if (string.IsNullOrWhiteSpace(cardTag))
                            continue;

                        if (!result.Contains(cardTag))
                            result.Add(cardTag);
                    }
                }
            }

            return result;
        }

        public void SortMissionCards()
        {
            var missionPage = _pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null)
                return;

            var missionCards = missionPage.AllCards.OfType<MissionCardModel>().ToList();
            if (missionCards.Count == 0)
                return;

            var sorted = missionCards
                .OrderByDescending(m => m.IsComplete)
                .ThenBy(m => m.IsComplete ? m.CompletedDate : DateTime.MinValue)
                .ThenBy(m => m.IsComplete ? DateTime.MaxValue : m.AvailableFromDate)
                .ToList();

            if (MissionOrderingIsCurrent(missionPage, missionCards, sorted))
                return;

            missionPage.AllCards.Clear();

            foreach (var mission in sorted)
            {
                if (mission == sorted[0]
                    || sorted[sorted.IndexOf(mission) - 1].AvailableFromDate.Date != mission.AvailableFromDate.Date)
                {
                    missionPage.AllCards.Add(new DateHeaderCardModel
                    {
                        Title = $"{TimeDisplayFormatter.FormatLocal(mission.AvailableFromDate.Date, "MMM-dd yyyy")} ({GetRelativeDateString(mission.AvailableFromDate)})",
                    });
                }

                missionPage.AllCards.Add(mission);
            }

            missionPage.ResetVisible();
        }

        public HomePageModel? FindPageContaining(ICardModel card)
        {
            return _pages.FirstOrDefault(page => page.AllCards.Contains(card));
        }

        public int GetCardPageIndex(ICardModel card)
        {
            var page = FindPageContaining(card);
            return page == null ? -1 : _pages.IndexOf(page);
        }

        private void ApplyMainQuestFilter(HomePageModel page)
        {
            switch (_mainQuestFilterMode)
            {
                case MainQuestFilterMode.None:
                    page.ResetVisible();
                    break;

                case MainQuestFilterMode.PositiveOnly:
                    page.ApplyFilter(IsPositiveMainQuestCard);
                    break;

                case MainQuestFilterMode.NegativeOnly:
                    page.ApplyFilter(IsNegativeMainQuestCard);
                    break;
            }
        }

        private static bool MissionOrderingIsCurrent(
            HomePageModel missionPage,
            IReadOnlyList<MissionCardModel> missionCards,
            IReadOnlyList<MissionCardModel> sorted)
        {
            if (missionCards.Count != sorted.Count)
                return false;

            var sameOrder = true;
            var hasDateHeaderCards =
                missionPage.AllCards.OfType<MissionCardModel>().Select(x => x.AvailableFromDate.Date).Distinct().Count() ==
                missionPage.AllCards.OfType<DateHeaderCardModel>().Select(y => y.Title).Distinct().Count();

            for (var i = 0; i < missionCards.Count; i++)
            {
                if (ReferenceEquals(missionCards[i], sorted[i]))
                    continue;

                sameOrder = false;
                break;
            }

            return sameOrder && hasDateHeaderCards;
        }

        private string GetRelativeDateString(DateTime dateTime)
        {
            var today = _clock.LocalNow.Date;

            if (dateTime.Date < today)
            {
                if (dateTime.Date == today.AddDays(-1))
                    return "Yesterday";

                return $"{(dateTime.Date - today).TotalDays * -1} Days Ago";
            }

            if (dateTime.Date == today)
                return "Today";

            if (dateTime.Date == today.AddDays(1))
                return "Tomorrow";

            return $"In {(dateTime.Date - today).TotalDays} Days";
        }

        private static bool IsPositiveMainQuestCard(ICardModel card)
        {
            return card is IActiveCardModel active
                && active.ValuePerMinute > 0;
        }

        private static bool IsNegativeMainQuestCard(ICardModel card)
        {
            return card is IActiveCardModel active
                && active.ValuePerMinute < 0;
        }

        private static bool GetBoolSetting(List<AcquiredSetting> settings, string key, bool defaultValue)
        {
            return settings.FirstOrDefault(x => x.SettingKey == key)?.BoolValue ?? defaultValue;
        }

        private static int GetIntSetting(List<AcquiredSetting> settings, string key, int defaultValue)
        {
            return settings.FirstOrDefault(x => x.SettingKey == key)?.IntValue ?? defaultValue;
        }

        private static HomePageModel CreatePage(PageDefinition definition)
        {
            return new HomePageModel(
                definition.Title,
                Enumerable.Empty<ICardModel>(),
                definition.Icon,
                definition.Title == "Dashboard" ? 8 : 12);
        }

        private static int IndexOfReference(
            IReadOnlyList<HomePageModel> pages,
            HomePageModel target)
        {
            for (var i = 0; i < pages.Count; i++)
            {
                if (ReferenceEquals(pages[i], target))
                    return i;
            }

            return -1;
        }

        private enum MainQuestFilterMode
        {
            None,
            PositiveOnly,
            NegativeOnly
        }

        private sealed class PageDefinition
        {
            public string Title { get; }
            public string Icon { get; }
            public int DefaultOrder { get; }
            public string ActiveSettingKey { get; }
            public string OrderSettingKey { get; }

            public PageDefinition(
                string title,
                string icon,
                int defaultOrder,
                string activeSettingKey,
                string orderSettingKey)
            {
                Title = title;
                Icon = icon;
                DefaultOrder = defaultOrder;
                ActiveSettingKey = activeSettingKey;
                OrderSettingKey = orderSettingKey;
            }
        }
    }

    internal readonly record struct HomePageReconciliationResult(
        bool LayoutChanged,
        HomePageModel? SelectedPage,
        int SelectedIndex);
}
