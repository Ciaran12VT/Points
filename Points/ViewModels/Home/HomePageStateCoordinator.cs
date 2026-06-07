using System.Collections.ObjectModel;
using Points.Global;
using Points.Models;
using Points.Services.Time;

namespace Points.ViewModels.Home
{
    internal sealed class HomePageStateCoordinator
    {
        private readonly ObservableCollection<HomePageModel> _pages;
        private readonly IClock _clock;
        private MainQuestFilterMode _mainQuestFilterMode = MainQuestFilterMode.None;

        public HomePageStateCoordinator(ObservableCollection<HomePageModel> pages, IClock clock)
        {
            _pages = pages;
            _clock = clock;
        }

        public void InitializePages(List<AcquiredSetting> settings)
        {
            _pages.Clear();
            _mainQuestFilterMode = MainQuestFilterMode.None;

            var pageDefinitions = new List<PageDefinition>
            {
                new(
                    title: "Dashboard",
                    icon: "𓃑",
                    defaultOrder: 1,
                    activeSettingKey: SettingKeys.DashboardActive,
                    orderSettingKey: SettingKeys.DashboardScreenOrder,
                    cardsFactory: () => Enumerable.Empty<ICardModel>()),

                new(
                    title: "Main Quest",
                    icon: "♻",
                    defaultOrder: 2,
                    activeSettingKey: SettingKeys.MainQuestActive,
                    orderSettingKey: SettingKeys.MainQuestScreenOrder,
                    cardsFactory: () => Enumerable.Empty<ICardModel>()),

                new(
                    title: "Mission",
                    icon: "⚑",
                    defaultOrder: 3,
                    activeSettingKey: SettingKeys.MissionActive,
                    orderSettingKey: SettingKeys.MissionScreenOrder,
                    cardsFactory: () => Enumerable.Empty<ICardModel>()),

                new(
                    title: "Budgets",
                    icon: "◷",
                    defaultOrder: 4,
                    activeSettingKey: SettingKeys.BudgetsActive,
                    orderSettingKey: SettingKeys.BudgetsScreenOrder,
                    cardsFactory: () => Enumerable.Empty<ICardModel>()),

                new(
                    title: "Challenges & Pinned Achievements",
                    icon: "★",
                    defaultOrder: 5,
                    activeSettingKey: SettingKeys.AchievementsActive,
                    orderSettingKey: SettingKeys.AchievementsScreenOrder,
                    cardsFactory: () => Enumerable.Empty<ICardModel>()),

                new(
                    title: "Arcs",
                    icon: "∿",
                    defaultOrder: 6,
                    activeSettingKey: SettingKeys.ArcsActive,
                    orderSettingKey: SettingKeys.ArcsScreenOrder,
                    cardsFactory: () => Enumerable.Empty<ICardModel>()),

                new(
                    title: "Goals",
                    icon: "☰",
                    defaultOrder: 7,
                    activeSettingKey: SettingKeys.GoalsActive,
                    orderSettingKey: SettingKeys.GoalsScreenOrder,
                    cardsFactory: () => Enumerable.Empty<ICardModel>())
            };

            var activePagesInOrder = pageDefinitions
                .Select(def => new
                {
                    Definition = def,
                    IsActive = GetBoolSetting(settings, def.ActiveSettingKey, true),
                    ScreenOrder = GetIntSetting(settings, def.OrderSettingKey, def.DefaultOrder)
                })
                .Where(x => x.IsActive)
                .OrderBy(x => x.ScreenOrder)
                .ThenBy(x => x.Definition.DefaultOrder)
                .ToList();

            foreach (var page in activePagesInOrder)
            {
                _pages.Add(new HomePageModel(
                    page.Definition.Title,
                    page.Definition.CardsFactory(),
                    page.Definition.Icon,
                    page.Definition.Title == "Dashboard" ? 8 : 12)); // The dashboard icon is slightly too big, so we use a smaller font size for it
            }
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
            public Func<IEnumerable<ICardModel>> CardsFactory { get; }

            public PageDefinition(
                string title,
                string icon,
                int defaultOrder,
                string activeSettingKey,
                string orderSettingKey,
                Func<IEnumerable<ICardModel>> cardsFactory)
            {
                Title = title;
                Icon = icon;
                DefaultOrder = defaultOrder;
                ActiveSettingKey = activeSettingKey;
                OrderSettingKey = orderSettingKey;
                CardsFactory = cardsFactory;
            }
        }
    }
}
