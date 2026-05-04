using Microsoft.Maui.Controls;
using Points.Global;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Views.Budgets;
using Points.Views.Missions;
using Points.Views.Sc;
using Points.Views.Tat;
using Points.Views.Trackers;
using Points.Views.Shared;

namespace Points.ViewModels.Home
{
    internal sealed class HomeCardWorkflowCoordinator
    {
        private readonly ILockService _locks;
        private readonly IActivityService _activity;
        private readonly IAchievementService _achievements;
        private readonly IUdmdService _udmd;
        private readonly IClock _clock;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly HomeCardLifecycleCoordinator _cardLifecycle;
        private readonly Func<List<string>> _getTags;
        private readonly Func<List<IActiveCardModel>> _getActiveCardModels;

        public HomeCardWorkflowCoordinator(
            ILockService locks,
            IActivityService activity,
            IAchievementService achievements,
            IUdmdService udmd,
            IClock clock,
            ITimeZoneService timeZoneService,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            HomeCardLifecycleCoordinator cardLifecycle,
            Func<List<string>> getTags,
            Func<List<IActiveCardModel>> getActiveCardModels)
        {
            _locks = locks;
            _activity = activity;
            _achievements = achievements;
            _udmd = udmd;
            _clock = clock;
            _timeZoneService = timeZoneService;
            _navigation = navigation;
            _dialogs = dialogs;
            _cardLifecycle = cardLifecycle;
            _getTags = getTags;
            _getActiveCardModels = getActiveCardModels;
        }

        public async Task AddCardFlowAsync(ICardModel? model, HomePageModel targetPage, bool openDetails)
        {
            if (targetPage == null)
                return;

            model ??= await CreateModelForPageAsync(targetPage);

            if (model == null)
                return;

            if (!openDetails)
            {
                _cardLifecycle.CommitCardToPage(targetPage, model);
                return;
            }

            await OpenDetailsForModelAsync(targetPage, model);
        }

        public async Task OpenDetailsForModelAsync(HomePageModel page, ICardModel model)
        {
            if (model is ScCardModel sc)
            {
                await _navigation.PushAsync(
                    new ScDetailsPage(
                        sc,
                        saved => _cardLifecycle.CommitCardToPage(page, saved),
                        deleted => _cardLifecycle.DeleteCardFromPageAndDbAsync(page, deleted),
                        card => _cardLifecycle.WouldArchiveOnDeleteAsync(card),
                        _getTags(),
                        _achievements,
                        _activity,
                        _udmd,
                        _clock,
                        _timeZoneService,
                        _navigation,
                        _dialogs
                    )
                );
                return;
            }

            if (model is TatCardModel tat)
            {
                var dependencyOptions = BuildDependencyTaskOptions();

                await _navigation.PushAsync(
                    new TatDetailsPage(
                        tat,
                        saved => _cardLifecycle.CommitCardToPage(page, saved),
                        deleted => _cardLifecycle.DeleteCardFromPageAndDbAsync(page, deleted),
                        card => _cardLifecycle.WouldArchiveOnDeleteAsync(card),
                        _getTags(),
                        _locks,
                        _activity,
                        _udmd,
                        dependencyOptions,
                        _clock,
                        _timeZoneService,
                        _navigation,
                        _dialogs
                    )
                );
                return;
            }

            if (model is MissionCardModel mission)
            {
                await _navigation.PushAsync(
                    new MissionDetailsPage(
                        mission,
                        saved => _cardLifecycle.CommitCardToPage(page, saved),
                        onDelete: _cardLifecycle.DeleteMission,
                        onFail: _cardLifecycle.FailMission,
                        _getTags(),
                        _activity,
                        _udmd,
                        _clock,
                        _timeZoneService,
                        _navigation,
                        _dialogs
                    )
                );
                return;
            }

            if (model is BudgetCardModel budget)
            {
                await _navigation.PushAsync(
                    new BudgetDetailsPage(
                        budget,
                        saved => _cardLifecycle.CommitCardToPage(page, saved),
                        deleted => _cardLifecycle.DeleteCardFromPageAndDbAsync(page, deleted),
                        card => _cardLifecycle.WouldArchiveOnDeleteAsync(card),
                        _getTags(),
                        _udmd,
                        _clock,
                        _timeZoneService,
                        _navigation,
                        _dialogs
                    )
                );
                return;
            }

            if (model is ValueTrackerCardModel valueTracker)
            {
                await _navigation.PushAsync(
                    new ValueTrackerDetailsPage(
                        valueTracker,
                        saved => _cardLifecycle.CommitCardToPage(page, saved),
                        deleted => _cardLifecycle.DeleteCardFromPageAndDbAsync(page, deleted),
                        card => _cardLifecycle.WouldArchiveOnDeleteAsync(card),
                        onCancelled: () => { },
                        udmd: _udmd,
                        clock: _clock,
                        navigation: _navigation,
                        dialogs: _dialogs
                    )
                );
                return;
            }

            if (model is EventTrackerCardModel eventTracker)
            {
                await _navigation.PushAsync(
                    new EventTrackerDetailsPage(
                        eventTracker,
                        saved => _cardLifecycle.CommitCardToPage(page, saved),
                        deleted => _cardLifecycle.DeleteCardFromPageAndDbAsync(page, deleted),
                        card => _cardLifecycle.WouldArchiveOnDeleteAsync(card),
                        onCancelled: () => { },
                        udmd: _udmd,
                        clock: _clock,
                        navigation: _navigation,
                        dialogs: _dialogs
                    )
                );
            }
        }

        private async Task<ICardModel?> CreateModelForPageAsync(HomePageModel page)
        {
            if (page.Name == "Main Quest")
            {
                var choice = await _dialogs.DisplayActionSheetAsync(
                    "Add Card",
                    "Cancel",
                    null,
                    "Time-At-Task",
                    "Step-Completion");

                return choice switch
                {
                    "Time-At-Task" => CreateDefaultTat(),
                    "Step-Completion" => CreateDefaultSc(),
                    _ => null
                };
            }

            if (page.Name == "Mission")
                return CreateDefaultMission();

            if (page.Name == "Budgets")
                return CreateDefaultBudget();

            if (page.Name == "Arcs")
            {
                var choice = await _dialogs.DisplayActionSheetAsync(
                    "Add Card",
                    "Cancel",
                    null,
                    "Value Tracker",
                    "Event Tracker");

                return choice switch
                {
                    "Value Tracker" => CreateDefaultValueTracker(),
                    "Event Tracker" => CreateDefaultEventTracker(),
                    _ => null
                };
            }

            return null;
        }

        private List<DependencyTaskOption> BuildDependencyTaskOptions()
        {
            return _getActiveCardModels()
                .Where(c => c.CardID > 0)
                .GroupBy(c => c.CardID)
                .Select(g => g.First())
                .OrderBy(c => c.Title)
                .Select(c => new DependencyTaskOption
                {
                    CardId = c.CardID,
                    Title = c.Title
                })
                .ToList();
        }

        private static TatCardModel CreateDefaultTat()
        {
            return new TatCardModel
            {
                Title = "",
                Status = "In-Progress",
                Tags = "",
                Description = "",
                ValuePerMinute = 1.0
            };
        }

        private static ScCardModel CreateDefaultSc()
        {
            return new ScCardModel
            {
                Title = "",
                Status = "In-Progress",
                Tags = "",
                Description = "",
                ValuePerMinute = 1.0
            };
        }

        private MissionCardModel CreateDefaultMission()
        {
            var now = _clock.LocalNow;

            var mission = new MissionCardModel
            {
                Title = "",
                Status = "In-Progress",
                Tags = "",
                SubType = MissionSubType.Stable,
                Value = 0,
                CreatedDate = _clock.UtcNow,
                AvailableFromDate = now,
                DueDate = now.AddDays(1),
                Description = ""
            };

            SettingsProvider.ApplyMissionDefaults(mission, now);
            return mission;
        }

        private BudgetCardModel CreateDefaultBudget()
        {
            return new BudgetCardModel
            {
                Title = "",
                Status = "In-Progress",
                Tags = "",
                Currency = "Kcal",
                ExchangeRate = 0.01,
                StartDate = _clock.LocalNow,
                InitialBalance = 0
            };
        }

        private ValueTrackerCardModel CreateDefaultValueTracker()
        {
            var now = _clock.LocalNow;

            return new ValueTrackerCardModel
            {
                CreatedDate = now.Date,
                ScheduleEvery = 1,
                ScheduleUnit = "Week",
                Unit = "Values"
            };
        }

        private EventTrackerCardModel CreateDefaultEventTracker()
        {
            var now = _clock.LocalNow;

            return new EventTrackerCardModel
            {
                CreatedDate = now.Date,
                GroupByPeriod = "Day",
                RangeStart = now,
                Unit = "Events"
            };
        }
    }
}
