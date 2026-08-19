using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Points.Global;
using Points.Models;
using Points.Services.Time;
using Points.ViewModels.Home;
using Xunit;

namespace Points.Tests.Home;

public sealed class HomePageStateCoordinatorTests
{
    [Fact]
    public void ReconcilePages_RepeatedSettingsPreserveIdentityWithoutCollectionChanges()
    {
        var pages = new ObservableCollection<HomePageModel>();
        var subject = new HomePageStateCoordinator(pages, new FixedClock());

        subject.ReconcilePages([], null, 0);
        var originalPages = pages.ToArray();
        var changes = new List<NotifyCollectionChangedAction>();
        pages.CollectionChanged += (_, args) => changes.Add(args.Action);

        var result = subject.ReconcilePages([], originalPages[2], 2);

        Assert.False(result.LayoutChanged);
        Assert.Same(originalPages[2], result.SelectedPage);
        Assert.Equal(originalPages.Length, pages.Count);
        Assert.All(originalPages.Select((page, index) => (page, index)),
            pair => Assert.Same(pair.page, pages[pair.index]));
        Assert.Empty(changes);
    }

    [Fact]
    public void ReconcilePages_ReordersAndDisablesWithoutResetAndReusesCanonicalPages()
    {
        var pages = new ObservableCollection<HomePageModel>();
        var subject = new HomePageStateCoordinator(pages, new FixedClock());
        subject.ReconcilePages([], null, 0);

        var dashboard = pages.Single(page => page.Name == "Dashboard");
        var mission = pages.Single(page => page.Name == "Mission");
        var collectionActions = new List<NotifyCollectionChangedAction>();
        pages.CollectionChanged += (_, args) => collectionActions.Add(args.Action);

        var reordered = subject.ReconcilePages(
        [
            IntSetting(SettingKeys.MissionScreenOrder, 1),
            IntSetting(SettingKeys.DashboardScreenOrder, 3)
        ], dashboard, 0);

        Assert.True(reordered.LayoutChanged);
        Assert.Same(mission, pages[0]);
        Assert.Same(dashboard, reordered.SelectedPage);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, collectionActions);

        collectionActions.Clear();
        var withoutMission = subject.ReconcilePages(
        [
            BoolSetting(SettingKeys.MissionActive, false)
        ], mission, 0);

        Assert.DoesNotContain(mission, pages);
        Assert.Same(pages[0], withoutMission.SelectedPage);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, collectionActions);

        subject.ReconcilePages([], null, 0);
        Assert.Same(mission, pages.Single(page => page.Name == "Mission"));
    }

    [Fact]
    public void ReconcilePages_RemovingSelectionUsesSameSlotThenPreviousLast()
    {
        var pages = new ObservableCollection<HomePageModel>();
        var subject = new HomePageStateCoordinator(pages, new FixedClock());
        subject.ReconcilePages([], null, 0);
        var mission = pages.Single(page => page.Name == "Mission");
        var missionIndex = pages.IndexOf(mission);

        var result = subject.ReconcilePages(
        [
            BoolSetting(SettingKeys.MissionActive, false)
        ], mission, missionIndex);

        Assert.Equal("Budgets", result.SelectedPage?.Name);
        Assert.Equal(missionIndex, result.SelectedIndex);

        var goals = pages.Single(page => page.Name == "Goals");
        var goalsIndex = pages.IndexOf(goals);
        result = subject.ReconcilePages(
        [
            BoolSetting(SettingKeys.MissionActive, false),
            BoolSetting(SettingKeys.GoalsActive, false)
        ], goals, goalsIndex);

        Assert.Equal("Arcs", result.SelectedPage?.Name);
        Assert.Equal(pages.Count - 1, result.SelectedIndex);
    }

    [Fact]
    public void ReconcilePages_AllDisabledReturnsNullSelection()
    {
        var pages = new ObservableCollection<HomePageModel>();
        var subject = new HomePageStateCoordinator(pages, new FixedClock());

        var result = subject.ReconcilePages(
        [
            BoolSetting(SettingKeys.DashboardActive, false),
            BoolSetting(SettingKeys.MainQuestActive, false),
            BoolSetting(SettingKeys.MissionActive, false),
            BoolSetting(SettingKeys.BudgetsActive, false),
            BoolSetting(SettingKeys.AchievementsActive, false),
            BoolSetting(SettingKeys.ArcsActive, false),
            BoolSetting(SettingKeys.GoalsActive, false)
        ], null, 0);

        Assert.Empty(pages);
        Assert.Null(result.SelectedPage);
        Assert.Equal(-1, result.SelectedIndex);
    }

    [Fact]
    public void ReconcilePages_DuplicateOrdersUseCanonicalDefaultOrder()
    {
        var pages = new ObservableCollection<HomePageModel>();
        var subject = new HomePageStateCoordinator(pages, new FixedClock());

        subject.ReconcilePages(
        [
            IntSetting(SettingKeys.DashboardScreenOrder, 4),
            IntSetting(SettingKeys.MainQuestScreenOrder, 4),
            IntSetting(SettingKeys.MissionScreenOrder, 4)
        ], null, 0);

        Assert.Equal("Dashboard", pages[0].Name);
        Assert.Equal("Main Quest", pages[1].Name);
        Assert.Equal("Mission", pages[2].Name);
    }

    [Fact]
    public void ReplaceCards_ReusesCollectionsAndDoesNotAccumulatePreviousSeed()
    {
        var page = new HomePageModel("Main Quest", [], "", 12);
        var allCards = page.AllCards;
        var visibleCards = page.VisibleCards;
        var first = new TatCardModel { CardID = 1, Title = "First" };
        var second = new TatCardModel { CardID = 2, Title = "Second" };

        page.ReplaceCards([first]);
        page.ReplaceCards([second]);

        Assert.Same(allCards, page.AllCards);
        Assert.Same(visibleCards, page.VisibleCards);
        Assert.Same(second, Assert.Single(page.AllCards));
        Assert.Same(second, Assert.Single(page.VisibleCards));
    }

    [Fact]
    public void SortMissionCards_AddsOneHeaderPerDateAndKeepsStableOrder()
    {
        var pages = new ObservableCollection<HomePageModel>();
        var clock = new FixedClock();
        var subject = new HomePageStateCoordinator(pages, clock);
        subject.ReconcilePages([], null, 0);
        var missionPage = pages.Single(page => page.Name == "Mission");
        var later = new MissionCardModel
        {
            CardID = 2,
            Title = "Later",
            AvailableFromDate = clock.LocalNow.Date.AddDays(1)
        };
        var today = new MissionCardModel
        {
            CardID = 1,
            Title = "Today",
            AvailableFromDate = clock.LocalNow.Date
        };

        missionPage.ReplaceCards([later, today]);
        subject.SortMissionCards();

        Assert.Collection(
            missionPage.AllCards,
            item => Assert.IsType<DateHeaderCardModel>(item),
            item => Assert.Same(today, item),
            item => Assert.IsType<DateHeaderCardModel>(item),
            item => Assert.Same(later, item));
        Assert.Equal(missionPage.AllCards, missionPage.VisibleCards);
    }

    private static AcquiredSetting BoolSetting(string key, bool value) =>
        new() { SettingKey = key, BoolValue = value };

    private static AcquiredSetting IntSetting(string key, int value) =>
        new() { SettingKey = key, IntValue = value };

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        public DateTime LocalNow => UtcNow.ToLocalTime();
        public DateTimeOffset UtcNowOffset => new(UtcNow);
    }
}
