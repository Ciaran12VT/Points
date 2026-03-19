using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Points.Models;
using Points.Global;
using Points.Services;
using System.Diagnostics;
using Points.Views.Details;
using System.Windows.Input;
using System.Text.Json;
using Points.Views.Shared;
using Points.Services.Locks;
using CommunityToolkit.Maui.Views;
using Points.Views.Popups;
using Points.Evaluators;
using Points.Services.Sqlite.Services.Interfaces;

namespace Points.ViewModels
{
    public class HomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly IActiveCardNotificationService _activeCardNotificationService;
        private IDbService _db;
        private IAlarmScheduler _alarmScheduler;

        private readonly SemaphoreSlim _achievementTickGate = new(1, 1);

        #region Commands
        public Command<IActiveCardModel> ActivateCardCommand { get; }

        public ICommand ShortcutClickedCommand => new Command<ShortcutModel>(OnShortcutClicked);
        private void OnShortcutClicked(ShortcutModel? shortcut)
        {
            if (shortcut == null) return;

            ScrollToAnyCardByIdRequested?.Invoke(shortcut.TargetCardId);
        }

        public Command AddCardCommand { get; }
        public Command ScrollToActiveCardCommand { get; }
        public Command FilterByTagCommand { get; }

        public Action<IActiveCardModel>? ScrollToCardRequested;

        public Action<long>? ScrollToAnyCardByIdRequested;
        public Command FilterPositiveCommand { get; }
        public Command FilterNegativeCommand { get; }
        public Command ClearFiltersCommand { get; }
        public Command SortByLastActiveCommand { get; }
        public Command OpenAchievementsCommand { get; }

        public Command OpenDateRangePickerViewCommand { get; }
        public Command OpenPlannerViewCommand { get; }
        public Command OpenSettingsCommand { get; }
        public Command OpenReportsCommand { get; }

        public Command ScrollToDashboardCommand { get; }

        public Command<ShortcutModel> OpenShortcutDetailsCommand { get; }

        public ICommand AddTrackerValueCommand { get; }

        #endregion

        #region Fields


        private bool _isTopToolbarVisible = false;
        public bool IsTopToolbarVisible
        {
            get => _isTopToolbarVisible;
            set
            {
                if (_isTopToolbarVisible == value)
                    return;

                _isTopToolbarVisible = value;
                OnPropertyChanged(nameof(IsTopToolbarVisible));
            }
        }

        public Command ToggleTopToolbarCommand { get; }



        //Used to check if there is an card currenty active
        public bool HasActiveCard => _activeCard is not null;

        //Returns the Carousel page that is currently displayed
        private HomePageModel CurrentPage => Pages[Math.Clamp(Position, 0, Pages.Count - 1)];

        //Returns the current time. Used for live updateding bound fields every second
        public DateTime _now = DateTime.Now;
        public DateTime Now
        {
            get => _now;
            set
            {
                if (_now == value) return;
                _now = value;
                OnPropertyChanged();
            }
        }

        //A collection of the Carousel Pages (Main Quest, Mission, Budgets)
        public ObservableCollection<HomePageModel> Pages { get; } = new();
        
        //Filter options for the Main Quest Page
        private enum MainQuestFilterMode
        {
            None,
            PositiveOnly,
            NegativeOnly
        }

        private MainQuestFilterMode _mainQuestFilterMode = MainQuestFilterMode.None;

        //Returns a formatted date string for the app header
        public string HeaderDate
        {
            get
            {
                if(GlobalVariables.RangeStart.Date == GlobalVariables.RangeEnd.Date)
                {
                    return GlobalVariables.RangeStart.Date.ToString("MMM-dd-yyyy");
                }
                else
                {
                    return $"{GlobalVariables.RangeStart.Date.ToString("MMM-dd")} - {GlobalVariables.RangeEnd.Date.ToString("MMM-dd")}";
                }
            }
        }

        //ActivePhaseName
        public string ActivePhaseName
        {
            get
            {
                try
                {
                    // IMPORTANT: log both HasActiveCard and the runtime type
                    System.Diagnostics.Debug.WriteLine(
                        $"ActivePhaseName evaluated. HasActiveCard={HasActiveCard}, _activeCardType={_activeCard?.GetType().FullName ?? "null"}"
                    );

                    if (!HasActiveCard)
                        return "Dead Air";

                    return _activeCard?.Title ?? "";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ActivePhaseName threw: {ex}");
                    return "";
                }
            }
        }
        public string ActivePhaseColor
        {
            get
            {
                try
                {
                    // IMPORTANT: log both HasActiveCard and the runtime type
                    System.Diagnostics.Debug.WriteLine(
                        $"ActivePhaseName evaluated. HasActiveCard={HasActiveCard}, _activeCardType={_activeCard?.GetType().FullName ?? "null"}"
                    );

                    if (!HasActiveCard) return "Gray";

                    return _activeCard?.ValuePerMinute >= 0 ? "Green" : "Red";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ActivePhaseName threw: {ex}");
                    return "Gray";
                }
            }
        }

        //Provides the color for the Global Value (top-right total value of app) based on its current value
        public Color GlobalValueColor
        {
            get
            {
                if (TopRightValue < 0) return Colors.Red;
                if (TopRightValue < 100) return Colors.Orange;
                return Colors.Green;
            }
        }

        //Calculates if there is a Rotting missing currently available and already in the negative. Is used to check if we should add the red ! before the Global Value
        public bool HasNegativeAvailableMission
        {
            get
            {
                var now = DateTime.Now;

                var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
                if (missionPage == null) return false;

                foreach (var m in missionPage.AllCards.OfType<MissionCardModel>())
                {
                    if (m.IsComplete) continue;
                    if (now < m.AvailableFromDate) continue;

                    var current = m.GetCurrentValue(now);
                    if (current < 0)
                        return true;
                }

                return false;
            }
        }

        //The index of the currently displayed carousel page. Setting this here changes the page displayed.
        private int _position;
        public int Position
        {
            get => _position;
            set
            {
                if (_position == value) return;
                _position = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPage));

                SetSelectedPageIcon();

                if (Pages[value].Name == "Planners") _ = ReloadPlannersAsync();

                if (Pages[value].IsDashboard) _ = ReloadDashboardAsync();
            }
        }

        public ICommand NavigationItemClickedCommand => new Command<HomePageModel>(OnNavigationItemClicked);

        private void SetSelectedPageIcon()
        {
            foreach (var page in Pages)
            {
                if(page == Pages[Position])
                {
                    page.BackColor = Colors.Green;
                }
                else
                {
                    page.BackColor = Colors.Black;
                }
                page.RaisePropertyChanged("BackColor");
            }
        }

        private void OnNavigationItemClicked(HomePageModel hpm)
        {
            int i = Pages.IndexOf(hpm);
            if(i > -1)
            {
                Position = i;
            }
        }


        //The value of the Global Value (also known as the "top right value")
        private double _topRightValue;
        public double TopRightValue
        {
            get => _topRightValue;
            set
            {
                if (Math.Abs(_topRightValue - value) < 0.0000001) return;
                _topRightValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GlobalValueColor));
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
                OnPropertyChanged(nameof(TopRightValue));
            }
        }

        //The start range, should probably be removed and the GlobalVariables.RangeStart used directly
        private DateTime _rangeStart = GlobalVariables.RangeStart;
        public DateTime RangeStart
        {
            get => _rangeStart;
            set
            {
                if (_rangeStart == value) return;
                _rangeStart = value;
                OnPropertyChanged();
            }
        }

        //The end range, should probably be removed and the GlobalVariables.RangeEnd used directly
        private DateTime _rangeEnd = GlobalVariables.RangeEnd;
        public DateTime RangeEnd
        {
            get => _rangeEnd;
            set
            {
                if (_rangeEnd == value) return;
                _rangeEnd = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GlobalValueColor));
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
            }
        }

        public List<IActiveCardModel> GetActiveCardModels()
        {
            var mainQuest = Pages.First(p => p.Name == "Main Quest");
            var mission = Pages.First(p => p.Name == "Mission");

            var merge = new List<IActiveCardModel>();
            merge.AddRange(mainQuest.AllCards.OfType<IActiveCardModel>());
            merge.AddRange(mission.AllCards.OfType<IActiveCardModel>());

            return merge;
        }

        public IReadOnlyList<IActiveCardModel> ActiveCardsForLocks => GetActiveCardModels();

        //A reference to the currenlty active card, if there is any
        private IActiveCardModel? _activeCard;

        public Task? Initialization { get; private set; }


        #endregion

        public HomeViewModel(IDbService db, IActiveCardNotificationService activeCardNotificationService, IAlarmScheduler alarmScheduler)
        {
            _activeCardNotificationService = activeCardNotificationService;
            _db = db;
            _alarmScheduler = alarmScheduler;

            // Commands
            ActivateCardCommand = new Command<IActiveCardModel>(RequestActivate);
            AddCardCommand = new Command(async () => await AddCardAsync());
            FilterPositiveCommand = new Command(ApplyPositiveFilter);
            FilterNegativeCommand = new Command(ApplyNegativeFilter);
            ClearFiltersCommand = new Command(ClearFilters);
            ScrollToActiveCardCommand = new Command(RequestScrollToActiveCard);
            SortByLastActiveCommand = new Command(SortCardsByLastActive);
            FilterByTagCommand = new Command(async () => await FilterCardsByTag());
            OpenAchievementsCommand = new Command(async () => await OpenAchievementsAsync());
            OpenDateRangePickerViewCommand = new Command(async () => await OpenDateRangePickerViewAsync());
            OpenPlannerViewCommand = new Command(async () => await OpenPlannerViewAsync());
            OpenSettingsCommand = new Command(async () => await OpenSettingsAsync());
            OpenReportsCommand = new Command(async () => await OpenReportsAsync());
            OpenShortcutDetailsCommand = new Command<ShortcutModel>(async shortcut => await OpenShortcutDetailsAsync(shortcut));
            ScrollToDashboardCommand = new Command(() => Position = Pages.IndexOf(Pages.First(p => p.IsDashboard)));
            ToggleTopToolbarCommand = new Command(() =>
            {
                IsTopToolbarVisible = !IsTopToolbarVisible;
            });


            AddTrackerValueCommand = new Command<TrackerCardModel>(async (card) =>
            {
                if (card is ValueTrackerCardModel valueCard)
                {
                    var page = Shell.Current?.CurrentPage;
                    if (page == null) return;

                    var input = await page.DisplayPromptAsync("Add Value", "Enter a value:",
                        accept: "OK", cancel: "Cancel", keyboard: Keyboard.Numeric);

                    if (string.IsNullOrWhiteSpace(input)) return;

                    if (!double.TryParse(input, out var v))
                    {
                        await page.DisplayAlert("Invalid value", "Please enter a valid number.", "OK");
                        return;
                    }

                    valueCard.AddValue(v);
                    await _db.SaveCardModelAsync(valueCard);
                }
                else if (card is EventTrackerCardModel eventCard)
                {
                    eventCard.AddValue();
                    await _db.SaveCardModelAsync(eventCard);
                }

                
            });

            // Pages + mock data moved out of constructor logic
            InitializePages();        // defines pages (empty)
                                      //SeedMockCards();          // adds mocks via the SAME route as UI adds

            // kick off async load without awaiting
            Initialization = LoadAsync();

            // Start on Main Quest
            Position = 0;

            // Ensure mission ordering after seeding
            SortMissionCards();
        }

        private void WireLongPress(ICardModel card)
        {
            if (card is IActiveCardModel active)
            {
                active.LongPressRequested -= OnCardLongPressRequested; // prevent double subscribe
                active.LongPressRequested += OnCardLongPressRequested;
            }
        }

        private async void OnCardLongPressRequested(IActiveCardModel card)
        {
            if (card is null) return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = Shell.Current?.CurrentPage;
                if (page is null) return;

                // UI-only placeholder bounds/initial for now:
                // We will replace these with DB-derived min/max/initial later.
                var nowLocal = DateTime.Now;
                
                var activationLabel = card.IsActive ? "Ends at" : "Starts at";

                DateTime minUtc;

                if (card.IsActive)
                {
                    var startUtc = await _db.GetCurrentOpenActivityStartUtcAsync(card.CardID);
                    if (startUtc == null)
                        return; // should not happen if IsActive true

                    minUtc = startUtc.Value;
                }
                else
                {
                    var lastEndUtc = await _db.GetLastClosedActivityEndUtcAsync();
                    minUtc = lastEndUtc ?? DateTime.MinValue;
                }

                // Convert to local for UI
                var minLocal = minUtc.ToLocalTime();

                var maxLocal = nowLocal;              // typically Now (unless you want future times)

                var initialLocal = nowLocal.AddMinutes(-5);

                var chosenObj = await page.ShowPopupAsync(new EditActiveTimePopup(
                    activationTypeText: activationLabel,
                    selectedTime: initialLocal,
                    minTime: minLocal,
                    maxTime: maxLocal));

                // Popup returns:
                // - DateTime chosen (from Close(chosen))
                // - null if dismissed by tapping outside
                if (chosenObj is not DateTime chosenLocal)
                    return; // cancelled

                // Convert to UTC for DB + activity system
                // (Make sure Kind is Local before ToUniversalTime)
                var chosenUtc = DateTime.SpecifyKind(chosenLocal, DateTimeKind.Local).ToUniversalTime();

                // Now execute the same toggle path, but at chosenUtc.
                // Choose whether to prompt for TAT rate selection here:
                RequestActivate(card, chosenUtc);
            });
        }

        private async Task LoadAsync()
        {
            // Get seed data (mock now, sqlite later)
            var now = DateTime.Now;
            var seed = await _db.GetHomeSeedDataAsync(new TimeScopeRange(TimeScope.Daily, now).Start, new TimeScopeRange(TimeScope.Monthly, now).End);
            var allPlannerModels = await _db.GetPlannerModelsDataAsync();
            var openActivity = await _db.GetCurrentActiveActivityAsync();
            var shortcuts = await _db.GetDashboardShortcutsAsync();

            // Make sure we touch ObservableCollection on UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var dashboard = Pages.First(p => p.Name == "Dashboard");
                var mainQuest = Pages.First(p => p.Name == "Main Quest");
                var mission = Pages.First(p => p.Name == "Mission");
                var budgets = Pages.First(p => p.Name == "Budgets");
                var achievements = Pages.First(p => p.Name == "Challenges & Pinned Achievements");
                var trackers = Pages.First(p => p.Name == "Arcs");
                var planners = Pages.First(p => p.Name == "Planners");

                RebuildDashboardCells(dashboard, shortcuts);

                // Activate the card that was active when the app last closed (DB-authoritative)
                if (openActivity != null)
                {
                    var activeCard = Pages
                        .SelectMany(x => x.AllCards)
                        .OfType<IActiveCardModel>()
                        .FirstOrDefault(c => c.CardID == openActivity.CardID);

                    if (activeCard != null)
                    {
                        // Ensure the open activity is present in the in-memory list
                        var existing = activeCard.Activity.FirstOrDefault(a => a.Id == openActivity.Id);
                        if (existing == null)
                        {
                            activeCard.Activity.Add(openActivity);
                            (activeCard as ObservableObject)?.RaisePropertyChanged(nameof(IActiveCardModel.Activity));
                        }

                        activeCard.IsActive = true;
                        _activeCard = activeCard;

                        _activeCardNotificationService.UpdateActiveCardNotification(_activeCard);
                    }
                    else
                    {
                        _activeCard = null;
                        _activeCardNotificationService.UpdateActiveCardNotification(null);
                    }
                }
                else
                {
                    _activeCard = null;
                    _activeCardNotificationService.UpdateActiveCardNotification(null);
                }


                foreach (var c in seed.MainQuestCards)
                    CommitCardToPage(mainQuest, c, true);

                foreach (var c in seed.MissionCards)
                    CommitCardToPage(mission, c, true);

                foreach (var c in seed.BudgetCards)
                    CommitCardToPage(budgets, c, true);

                foreach (var c in seed.Achievements.Cast<AchievementCardModel>().Where(x => x.IsPinned))
                    CommitCardToPage(achievements, c, true);

                foreach (var c in seed.ValueTrackers)
                    CommitCardToPage(trackers, c, true);

                foreach (var c in seed.EventTrackers)
                    CommitCardToPage(trackers, c, true);

                SortMissionCards();
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
                OnPropertyChanged(nameof(GlobalValueColor));
                OnPropertyChanged(nameof(ActivePhaseName));
                OnPropertyChanged(nameof(ActivePhaseColor));

                // Load planner progress rows — separate DB calls since these
                // are not part of the home seed data
                var now = DateTime.Now;
                var allCards = seed.MainQuestCards;
                var enabledPlannerModels = allPlannerModels.Where(p => p.Enabled).ToList();

                foreach (var scope in new[] { TimeScope.Daily, TimeScope.Weekly, TimeScope.Monthly })
                {
                    var modelsForScope = enabledPlannerModels
                        .Where(p => p.TimeScope == scope)
                        .ToList();

                    if (modelsForScope.Count == 0) continue;

                    var rowVms = new List<PlannerProgressRowVm>();
                    foreach (var plannerModel in modelsForScope)
                    {
                        var card = allCards.FirstOrDefault(c => c.CardID == plannerModel.CardId);
                        if (card is null) continue;

                        var row = new PlannerProgressRowVm(card, plannerModel)
                        {
                            EnableCheckbox = false
                        };
                        rowVms.Add(row);
                    }

                    if (rowVms.Count == 0) continue;

                    var maxValue = rowVms.Max(r => Math.Max(r.TotalValue, r.CurrentValue.HasValue ? r.CurrentValue.Value : 0));

                    CommitCardToPage(planners, new DateHeaderCardModel { Title = scope.ToString() }, noDb: true);

                    foreach (var row in rowVms)
                    {
                        row.MaxValue = maxValue;
                        CommitCardToPage(planners, row, noDb: true);
                    }
                }

            });



            var scheduleables =
                seed.MainQuestCards
                    .OfType<IScheduleable>()
                    .Concat(seed.ValueTrackers.OfType<IScheduleable>())
                    .Concat(seed.MissionCards.OfType<IScheduleable>()) // if applicable
                    .ToList();

            var schedules = scheduleables
                .SelectMany(x => x.Schedules)
                .Where(s => s.IsEnabled)
                .ToList();

            await _alarmScheduler.ScheduleAllAsync(schedules);
        }

        private async Task ReloadPlannersAsync()
        {
            var now = DateTime.Now;
            var planners = Pages.First(p => p.Name == "Planners");

            List<DateTime> startDates = new()
            {
                new TimeScopeRange(TimeScope.Daily, now).Start,
                new TimeScopeRange(TimeScope.Weekly, now).Start,
                new TimeScopeRange(TimeScope.Monthly, now).Start
            };
            List<DateTime> endDates = new()
            {
                new TimeScopeRange(TimeScope.Daily, now).End,
                new TimeScopeRange(TimeScope.Weekly, now).End,
                new TimeScopeRange(TimeScope.Monthly, now).End
            };

            var allCards = await _db.GetMainQuestModelsDataAsync(
                startDates.Min(),
                endDates.Max());

            var allPlannerModels = await _db.GetPlannerModelsDataAsync();
            var enabledPlannerModels = allPlannerModels.Where(p => p.Enabled).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                planners.AllCards.Clear();

                foreach (var scope in new[] { TimeScope.Daily, TimeScope.Weekly, TimeScope.Monthly })
                {
                    var modelsForScope = enabledPlannerModels
                        .Where(p => p.TimeScope == scope)
                        .ToList();

                    if (modelsForScope.Count == 0) continue;

                    var rowVms = new List<PlannerProgressRowVm>();
                    foreach (var plannerModel in modelsForScope)
                    {
                        var card = allCards.FirstOrDefault(c => c.CardID == plannerModel.CardId);
                        if (card is null) continue;

                        var row = new PlannerProgressRowVm(card, plannerModel)
                        {
                            EnableCheckbox = false
                        };
                        rowVms.Add(row);
                    }

                    if (rowVms.Count == 0) continue;

                    var maxValue = rowVms.Max(r =>
                        Math.Max(r.TotalValue, r.CurrentValue.HasValue ? r.CurrentValue.Value : 0));

                    planners.AllCards.Add(new DateHeaderCardModel { Title = scope.ToString() });

                    foreach (var row in rowVms)
                    {
                        row.MaxValue = maxValue;
                        planners.AllCards.Add(row);
                    }
                }

                planners.ResetVisible();
            });
        }

        private async Task ReloadDashboardAsync()
        {
            var dashboard = Pages.First(p => p.IsDashboard);
            var shortcuts = await _db.GetDashboardShortcutsAsync();
            RebuildDashboardCells(dashboard, shortcuts);
        }

        private void RebuildDashboardCells(HomePageModel dashboardPage, List<ShortcutModel> shortcuts)
        {
            // Expected: each ShortcutModel has:
            // - ShortcutOrder
            // - TargetCardId
            // - IconChar
            // - Group (ShortcutGroupModel?) with GroupOrder + Color
            // If your model differs, adapt here.

            dashboardPage.DashboardCells.Clear();

            var orderedGroups = shortcuts
                .Where(s => s.Group != null)
                .GroupBy(s => s.Group!.ShortcutGroupId) // or GroupId
                .Select(g => new
                {
                    Group = g.First().Group!, // same group object
                    Items = g.OrderBy(s => s.ShortcutOrder).ToList()
                })
                .OrderBy(x => x.Group.ShortcutGroupOrder) // group order
                .ToList();

            foreach (var grp in orderedGroups)
            {
                // Add the group's items
                foreach (var s in grp.Items)
                {
                    dashboardPage.DashboardCells.Add(new DashboardCellModel
                    {
                        IsPlaceholder = false,
                        Shortcut = s
                    });
                }

                // Rule #3: pad to next row boundary (multiple of 4)
                int rem = dashboardPage.DashboardCells.Count % 4;
                if (rem != 0)
                {
                    int pad = 4 - rem;
                    for (int i = 0; i < pad; i++)
                    {
                        dashboardPage.DashboardCells.Add(new DashboardCellModel
                        {
                            IsPlaceholder = true,
                            Shortcut = null
                        });
                    }
                }
            }
        }

        #region Single-route add/commit pipeline

        /// <summary>
        /// UI entry: Add button.
        /// Uses the same pipeline: (create model) -> (open details if needed) -> (commit card to page) -> (post-commit hooks)
        /// </summary>
        private async Task AddCardAsync()
        {
            var page = CurrentPage;

            if (page.Name == "Dashboard")
            {
                await AddDashboardShortcutAsync();
                return;
            }

            await AddCardFlowAsync(model: null, targetPage: CurrentPage, openDetails: true);
        }

        /// <summary>
        /// Strong single-route guarantee:
        /// - If model is null: chooses a model based on the target page.
        /// - If openDetails is true: navigates to the relevant details page and commits from the save callback.
        /// - If openDetails is false: commits immediately (used for mock seeding).
        /// </summary>
        private async Task AddCardFlowAsync(ICardModel? model, HomePageModel targetPage, bool openDetails)
        {
            if (targetPage == null) return;

            // If the caller didn't supply a model, create one based on the page
            model ??= await CreateModelForPageAsync(targetPage);

            if (model == null) return;

            if (!openDetails)
            {
                CommitCardToPage(targetPage, model);
                return;
            }

            // Open details UI, and commit ONLY through the shared commit route
            await OpenDetailsForModelAsync(targetPage, model);
        }

        /// <summary>
        /// Page-based model creation (only place that decides "what model to use").
        /// For Main Quest, shows ActionSheet because there are multiple card types.
        /// </summary>
        private async Task<ICardModel?> CreateModelForPageAsync(HomePageModel page)
        {
            if (page.Name == "Main Quest")
            {
                var choice = await Shell.Current.DisplayActionSheet(
                    "Add Card",
                    "Cancel",
                    null,
                    "Time-At-Task",
                    "Step-Completion");

                if (choice == "Time-At-Task")
                {
                    return CreateDefaultTat();
                }
                else if (choice == "Step-Completion")
                {
                    return CreateDefaultSc();
                }

                return null;
            }

            if (page.Name == "Mission")
            {
                return CreateDefaultMission();
            }

            if (page.Name == "Budgets")
            {
                return CreateDefaultBudget();
            }

            if (page.Name == "Arcs")
            {
                var choice = await Shell.Current.DisplayActionSheet(
                    "Add Card",
                    "Cancel",
                    null,
                    "Value Tracker",
                    "Event Tracker");

                if (choice == "Value Tracker")
                {
                    return CreateDefaultValueTracker();
                }
                else if (choice == "Event Tracker")
                {
                    return CreateDefaultEventTracker();
                }

                return null;
            }

            return null;
        }

        /// <summary>
        /// Navigation to details page is here; commit only happens via CommitCardToPage.
        /// </summary>
        private async Task OpenDetailsForModelAsync(HomePageModel page, ICardModel model)
        {
            if (model is ScCardModel sc)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.ScDetailsPage(
                        sc,
                        saved => CommitCardToPage(page, saved),
                        deleted => RemoveCardFromPage(page, deleted),
                        GetTags(),
                        _db
                    )
                );
                return;
            }

            if (model is TatCardModel tat)
            {
                var dependencyOptions = BuildDependencyTaskOptions();

                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.TatDetailsPage(
                        tat,
                        saved => CommitCardToPage(page, saved),
                        deleted => RemoveCardFromPage(page, deleted),
                        GetTags(),
                        _db,
                        dependencyOptions
                    )
                );
                return;
            }

            if (model is MissionCardModel mission)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.MissionDetailsPage(
                        mission,
                        saved => CommitCardToPage(page, saved),
                        onDelete: m => DeleteMission(m),
                        onFail: m => FailMission(m),
                        GetTags(),
                        _db
                    )
                );
                return;
            }

            if (model is BudgetCardModel budget)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.BudgetDetailsPage(
                        budget,
                        saved => CommitCardToPage(page, saved),
                        deleted => RemoveCardFromPage(page, deleted),
                        GetTags()
                    )
                );
                return;
            }

            if (model is ValueTrackerCardModel valueTracker)
            {
                await Shell.Current.Navigation.PushAsync(
                    new ValueTrackerDetailsPage(
                        valueTracker,
                        saved => CommitCardToPage(page, saved),
                        onCancelled: () => { }
                    )
                );
                return;
            }

            if (model is EventTrackerCardModel eventTracker)
            {
                await Shell.Current.Navigation.PushAsync(
                    new EventTrackerDetailsPage(
                        eventTracker,
                        saved => CommitCardToPage(page, saved),
                        onCancelled: () => { }
                    )
                );
                return;
            }
        }

        private List<DependencyTaskOption> BuildDependencyTaskOptions()
        {
            var merge = GetActiveCardModels();

            return merge
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

        private async Task AddDashboardShortcutAsync()
        {
            // 1) Build card options for the details page
            var optionsByType = BuildShortcutOptionsByType();

            // Pick a sensible default target (MainQuest if possible, else first available)
            CardOption? defaultTarget =
                (optionsByType.TryGetValue(TargetCardType.MainQuest, out var mq) ? mq.FirstOrDefault() : null)
                ?? optionsByType.SelectMany(kvp => kvp.Value).FirstOrDefault();

            if (defaultTarget == null || defaultTarget.CardId <= 0)
            {
                await Shell.Current.DisplayAlert(
                    "No targets",
                    "No valid cards are loaded to target. Create a card first, then add a shortcut.",
                    "OK");
                return;
            }

            // 2) Load groups + shortcuts (for picker + default ordering)
            var groups = await _db.GetShortcutGroupsAsync();
            var shortcuts = await _db.GetDashboardShortcutsAsync();

            var existingGroupNames = groups
                .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                .OrderBy(g => g.ShortcutGroupOrder)
                .ThenBy(g => g.Name)
                .Select(g => g.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var nextShortcutOrder = shortcuts.Count == 0 ? 1 : shortcuts.Max(s => s.ShortcutOrder) + 1;

            // 3) New shortcut model defaults
            var model = new ShortcutModel
            {
                ShortcutId = 0,
                IconChar = "★",
                TargetCardId = defaultTarget.CardId,
                ShortcutOrder = nextShortcutOrder,
                ShortcutGroupId = 0,   // will be set after group upsert
                Group = new ShortcutGroupModel
                {
                    ShortcutGroupId = 0,
                    Name = "",
                    Color = Colors.Black,
                    ShortcutGroupOrder = 0
                }
            };

            // 4) Dashboard reload helper
            async Task ReloadDashboardAsync()
            {
                var updated = await _db.GetDashboardShortcutsAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var dashboard = Pages.First(p => p.Name == "Dashboard");
                    RebuildDashboardCells(dashboard, updated);
                });
            }

            // 5) Save callback (Action<ShortcutModel>), uses your DB methods
            Action<ShortcutModel> onSaved = saved =>
            {
                _ = Task.Run(async () =>
                {
                    // Upsert group from the edited model.Group (name/order/color)
                    if (saved.Group == null || string.IsNullOrWhiteSpace(saved.Group.Name))
                        throw new InvalidOperationException("Shortcut must have a Group with a Name before saving.");

                    var persistedGroup = await _db.UpsertShortcutGroupAsync(saved.Group);

                    // Ensure FK is set
                    saved.ShortcutGroupId = persistedGroup.ShortcutGroupId;
                    saved.Group = persistedGroup;

                    // Save shortcut row
                    await _db.SaveShortcutAsync(saved);

                    // Refresh dashboard UI
                    await ReloadDashboardAsync();
                });
            };

            // 6) Delete callback
            Action<ShortcutModel> onDelete = deleted =>
            {
                _ = Task.Run(async () =>
                {
                    if (deleted.ShortcutId > 0)
                        await _db.DeleteShortcutAsync(deleted.ShortcutId);

                    await ReloadDashboardAsync();
                });
            };

            // 7) Navigate (page constructs VM internally)
            await Shell.Current.Navigation.PushAsync(
                new ShortcutDetailsPage(
                    model: model,
                    optionsByType: optionsByType,
                    existingGroups: groups,
                    onSaved: onSaved,
                    onDelete: onDelete,
                    defaultType: TargetCardType.MainQuest
                )
            );
        }

        /// <summary>
        /// Builds dropdown options for ShortcutDetails based on what is currently loaded into Pages.
        /// Excludes header-only pseudo-cards.
        /// </summary>
        private Dictionary<TargetCardType, List<CardOption>> BuildShortcutOptionsByType()
        {
            static bool IsHeaderCard(ICardModel c) =>
                c is DateHeaderCardModel ||
                c is TimeScopeHeaderCardModel; // if you have this type in your project

            List<CardOption> ToOptions(IEnumerable<ICardModel> cards) =>
                cards.Where(c => c != null && c.CardID > 0)
                     .Where(c => !IsHeaderCard(c))
                     .Select(c => new CardOption { CardId = c.CardID, Title = c.Title ?? "" })
                     .Where(o => !string.IsNullOrWhiteSpace(o.Title))
                     .OrderBy(o => o.Title)
                     .ToList();

            var dict = new Dictionary<TargetCardType, List<CardOption>>();

            var mainQuestPage = Pages.FirstOrDefault(p => p.Name == "Main Quest");
            var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
            var budgetsPage = Pages.FirstOrDefault(p => p.Name == "Budgets");
            var achPage = Pages.FirstOrDefault(p => p.Name == "Challenges & Pinned Achievements");
            var arcsPage = Pages.FirstOrDefault(p => p.Name == "Arcs");
            var plannersPage = Pages.FirstOrDefault(p => p.Name == "Planners");

            if (mainQuestPage != null)
                dict[TargetCardType.MainQuest] = ToOptions(mainQuestPage.AllCards);

            if (missionPage != null)
                dict[TargetCardType.Mission] = ToOptions(missionPage.AllCards);

            if (budgetsPage != null)
                dict[TargetCardType.Budget] = ToOptions(budgetsPage.AllCards);

            if (achPage != null)
                dict[TargetCardType.Achievement] = ToOptions(achPage.AllCards);

            if (arcsPage != null)
                dict[TargetCardType.Arc] = ToOptions(arcsPage.AllCards);

            if (plannersPage != null)
                dict[TargetCardType.Planner] = ToOptions(plannersPage.AllCards);

            // Ensure all enum values exist (so pickers don’t explode if a page is empty)
            foreach (TargetCardType t in Enum.GetValues(typeof(TargetCardType)))
                if (!dict.ContainsKey(t))
                    dict[t] = new List<CardOption>();

            return dict;
        }

        /// <summary>
        /// The ONE AND ONLY way a card gets added to a page.
        /// All callers (mock seeding + UI save callbacks) must use this.
        /// </summary>
        private void CommitCardToPage(HomePageModel page, ICardModel card, bool noDb = false)
        {
            if (page == null || card == null) return;

            // If card already exists (editing existing), don't duplicate
            // (Reference equality is the safest default here.)
            if (!page.AllCards.Contains(card))
            {
                page.AddCard(card);
            }

            WireLongPress(card);

            if (!noDb) CommitCardToDb(card);

            // Post-commit hooks centralized here
            AfterCardCommitted(page, card);
        }

        private void CommitCardToDb(ICardModel card)
        {
             _db.SaveCardModelAsync(card);
        }

        private void AfterCardCommitted(HomePageModel page, ICardModel card)
        {
            // Any per-page logic that must always happen after add/update
            if (page.Name == "Mission" && card is MissionCardModel)
            {
                SortMissionCards();
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
            }

            // For value/colour refresh, etc.
            OnPropertyChanged(nameof(GlobalValueColor));
        }

        private void RemoveCardFromPage(HomePageModel page, ICardModel card)
        {
            if (page == null || card == null) return;
            page.RemoveCard(card);

            if (page.Name == "Mission")
            {
                SortMissionCards();
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
            }
        }

        #endregion

        #region Default model factories (no side effects)

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

        private static MissionCardModel CreateDefaultMission()
        {
            var now = DateTime.Now;

            return new MissionCardModel
            {
                Title = "",
                Status = "In-Progress",                 // non-editable in form
                Tags = "",
                SubType = MissionSubType.Stable,
                Value = 0,
                CreatedDate = now,                      // auto-set to now
                AvailableFromDate = now,                // default now
                DueDate = now.AddDays(1),               // default now + 1 day
                Description = ""
            };
        }

        private static BudgetCardModel CreateDefaultBudget()
        {
            return new BudgetCardModel
            {
                Title = "",
                Status = "In-Progress",
                Tags = "",
                Currency = "Kcal",
                ExchangeRate = 0.01,
                StartDate = DateTime.Now,
                InitialBalance = 0
            };
        }

        private static ValueTrackerCardModel CreateDefaultValueTracker()
        {
            return new ValueTrackerCardModel
            {
                CreatedDate = DateTime.Today,
                ScheduleEvery = 1,
                ScheduleUnit = "Week",
                Unit = "Values"
            };
        }

        private static EventTrackerCardModel CreateDefaultEventTracker()
        {
            return new EventTrackerCardModel
            {
                CreatedDate = DateTime.Today,
                GroupByPeriod = "Day",
                RangeStart = DateTime.Now,
                Unit = "Events"
            };
        }

        #endregion

        #region Page + mock seeding (moved out of ctor; uses single route)

        private void InitializePages()
        {
            Pages.Clear();

            Pages.Add(new HomePageModel("Dashboard", Enumerable.Empty<ICardModel>(), "𓃑", 7));
            Pages.Add(new HomePageModel("Main Quest", Enumerable.Empty<IActiveCardModel>(), "♻", 12));
            Pages.Add(new HomePageModel("Mission", Enumerable.Empty<IActiveCardModel>(), "⚑", 12));
            Pages.Add(new HomePageModel("Budgets", Enumerable.Empty<IActiveCardModel>(), "◷", 12));
            Pages.Add(new HomePageModel("Challenges & Pinned Achievements", Enumerable.Empty<ICardModel>(), "★", 12));
            Pages.Add(new HomePageModel("Arcs", Enumerable.Empty<ICardModel>(), "∿", 12));
            Pages.Add(new HomePageModel("Planners", Enumerable.Empty<ICardModel>(), "☰", 12));
       
        }

        #endregion

        #region Methods (existing behavior preserved)

        public async void RequestActivate(IActiveCardModel card)
        {
            RequestActivate(card, null);
        }

        public async void RequestActivate(IActiveCardModel card, DateTime? nowUtc = null)
        {
            if (card == null) return;

            try
            {
                DateTime nowUtcNonNull = nowUtc ?? DateTime.UtcNow;

                if (LockEvaluator.IsLockedNow(card, nowUtcNonNull, GetActiveCardModels(), out var availableAt))
                {
                    var rem = LockEvaluator.FormatRemaining(nowUtcNonNull, availableAt);

                    // Choose your UX:
                    // 1) quick error label in page, or
                    // 2) DisplayAlert, or
                    // 3) toast/snackbar
                    await Shell.Current.DisplayAlert("Locked", $"This card is locked. Available in {rem}.", "OK");
                    return;
                }

                // Snapshot values for the activity we might open
                string rateName = "Base Rate";
                double valuePerMinute = card.ValuePerMinute;

                // If TAT has selectable rates, prompt user BEFORE toggling
                if (card is TatCardModel tat && tat.ValueRates.Count > 0 && !card.IsActive)
                {
                    List<string> rateNames = ["Base Rate", .. tat.ValueRates.Select(x => x.RateName)];

                    var choice = await Shell.Current.DisplayActionSheet(
                        "Choose Rate",
                        "Cancel",
                        null,
                        rateNames.ToArray()
                    );

                    if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
                        return;

                    tat.SelectedValueRateModel =
                        choice == "Base Rate"
                            ? null
                            : tat.ValueRates.FirstOrDefault(x => x.RateName == choice);

                    rateName = tat.SelectedValueRateModel?.RateName ?? "Base Rate";
                    valuePerMinute = tat.SelectedValueRateModel?.ValuePerMinute ?? tat.ValuePerMinute;
                }
                else if (card is TatCardModel tatNoRates)
                {
                    // Persist base rate snapshot consistently
                    rateName = tatNoRates.SelectedValueRateModel?.RateName ?? "Base Rate";
                    valuePerMinute = tatNoRates.SelectedValueRateModel?.ValuePerMinute ?? tatNoRates.ValuePerMinute;
                }

                // DB authoritative toggle
                var result = await _db.ToggleActivityAsync(
                    cardId: card.CardID,
                    utcNow: nowUtcNonNull,
                    valueRateName: rateName,
                    valuePerMinute: valuePerMinute);

                // Deterministic lookup across all loaded cards
                IActiveCardModel? ResolveCard(long cardId) =>
                    Pages.SelectMany(x => x.AllCards)
                         .OfType<IActiveCardModel>()
                         .FirstOrDefault(c => c.CardID == cardId);

                static void UpsertActivity(IActiveCardModel target, ActivityModel activity)
                {
                    var existing = target.Activity.FirstOrDefault(a => a.Id == activity.Id);

                    if (existing == null)
                    {
                        target.Activity.Add(activity);
                    }
                    else
                    {
                        existing.CardID = activity.CardID;
                        existing.StartDate = activity.StartDate;
                        existing.EndDate = activity.EndDate;
                        existing.RateName = activity.RateName;
                        existing.ValuePerMinute = activity.ValuePerMinute;
                    }
                }

                static void RefreshTatComputedProps(IActiveCardModel model)
                {
                    if (model is TatCardModel tatModel)
                    {
                        tatModel.RaisePropertyChanged(nameof(TatCardModel.ShowRateNameOnCard));
                        tatModel.RaisePropertyChanged(nameof(TatCardModel.SelectedRateName));
                        tatModel.RaisePropertyChanged(nameof(TatCardModel.Activity));
                    }
                    else
                    {
                        // For MissionCardModel etc.
                        (model as ObservableObject)?.RaisePropertyChanged(nameof(IActiveCardModel.Activity));
                    }

                    (model as ObservableObject)?.RaisePropertyChanged(nameof(IActiveCardModel.IsActive));
                }


                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Apply closed activity (if any)
                    if (result.Closed != null)
                    {
                        var closedCard = ResolveCard(result.Closed.CardID);
                        if (closedCard != null)
                        {
                            UpsertActivity(closedCard, result.Closed);
                            closedCard.IsActive = false;
                            RefreshTatComputedProps(closedCard);
                        }
                    }

                    // Apply opened activity (if any)
                    if (result.Opened != null)
                    {
                        var openedCard = ResolveCard(result.Opened.CardID) ?? card;

                        UpsertActivity(openedCard, result.Opened);
                        openedCard.IsActive = true;
                        RefreshTatComputedProps(openedCard);

                        _activeCard = openedCard;
                    }
                    else
                    {
                        // Nothing opened => no active card
                        _activeCard = null;
                    }

                    // Update HomeViewModel computed properties
                    OnPropertyChanged(nameof(HasActiveCard));
                    OnPropertyChanged(nameof(ActivePhaseName));
                    OnPropertyChanged(nameof(ActivePhaseColor));
                });

                // Notification matches DB-authoritative active card
                _activeCardNotificationService.UpdateActiveCardNotification(_activeCard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                // Optional: show toast/alert
            }
        }


        private void ApplyPositiveFilter()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

            _mainQuestFilterMode =
                _mainQuestFilterMode == MainQuestFilterMode.PositiveOnly
                    ? MainQuestFilterMode.None
                    : MainQuestFilterMode.PositiveOnly;

            ApplyMainQuestFilter(page);
        }

        private void ApplyNegativeFilter()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

            _mainQuestFilterMode =
                _mainQuestFilterMode == MainQuestFilterMode.NegativeOnly
                    ? MainQuestFilterMode.None
                    : MainQuestFilterMode.NegativeOnly;

            ApplyMainQuestFilter(page);
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

        private void ClearFilters()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest" && page.Name != "Mission") return;

            page.ResetVisible();
            SortMissionCards();
        }

        private void SortCardsByLastActive()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

            page.SortCardsByLastActive();
        }

        private async Task FilterCardsByTag()
        {
            var choice = await Shell.Current.DisplayActionSheet(
                "Add Card",
                "Cancel",
                null,
                GetTags().ToArray()
            );

            if (!string.IsNullOrEmpty(choice))
            {
                foreach (var page in Pages)
                {
                    page.FilterCardsByTag(choice);
                }
            }
        }

        private List<string> GetTags()
        {
            var result = new List<string>();

            foreach (var page in Pages)
            {
                foreach (var card in page.AllCards)
                {
                    var cardTags = card.Tags.Replace('#', ' ').Replace(',', ' ')
                        .Split(' ')
                        .Select(x => x.Trim());

                    foreach (var cardTag in cardTags)
                    {
                        if (string.IsNullOrWhiteSpace(cardTag)) continue;

                        if (!result.Contains(cardTag))
                        {
                            result.Add(cardTag);
                        }
                    }
                }
            }

            return result;
        }

        internal void FilterCardsBySearchTerm(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                foreach (var page in Pages)
                {
                    page.FilterCardsBySearchTerm(input);
                }
            }
        }

        private bool IsPositiveMainQuestCard(ICardModel card)
        {
            return card is IActiveCardModel active
                && active.ValuePerMinute > 0;
        }

        private bool IsNegativeMainQuestCard(ICardModel card)
        {
            return card is IActiveCardModel active
                && active.ValuePerMinute < 0;
        }

        private void RequestScrollToActiveCard()
        {
            if (_activeCard == null)
                return;

            ScrollToCardRequested?.Invoke((IActiveCardModel)_activeCard);
        }

        private void SortMissionCards()
        {
            var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null) return;

            var missionCards = missionPage.AllCards.OfType<MissionCardModel>().ToList();
            if (missionCards.Count == 0) return;

            // Completed at top, ordered by CompletedDate (most recent first)
            // Others by AvailableFromDate (oldest -> newest)
            var sorted = missionCards
                .OrderByDescending(m => m.IsComplete) // true first
                .ThenBy(m => m.IsComplete ? m.CompletedDate : DateTime.MinValue)
                .ThenBy(m => m.IsComplete ? DateTime.MaxValue : m.AvailableFromDate)
                .ToList();

            // Check if there is any difference between missionPage.AllCards and sorted. If not, do not reset
            if (missionCards.Count == sorted.Count)
            {
                bool sameOrder = true;
                bool hasDateHeaderCards = 
                    missionPage.AllCards.OfType<MissionCardModel>().Select(x => x.AvailableFromDate.Date).Distinct().Count() ==
                    missionPage.AllCards.OfType<DateHeaderCardModel>().Select(y => y.Title).Distinct().Count();

                for (int i = 0; i < missionCards.Count; i++)
                {
                    if (!ReferenceEquals(missionCards[i], sorted[i]))
                    {
                        sameOrder = false;
                        break;
                    }
                }

                if (sameOrder && hasDateHeaderCards) return;
            }

            missionPage.AllCards.Clear();

            foreach (var m in sorted)
            {
                if (m == sorted[0] || sorted[sorted.IndexOf(m) - 1].AvailableFromDate.Date != m.AvailableFromDate.Date)
                {
                    var dateheadermodel = new DateHeaderCardModel()
                    {
                        Title = $"{m.AvailableFromDate.Date.ToString("MMM-dd yyyy")} ({GetRelativeDateString(m.AvailableFromDate)})",
                    };
                    missionPage.AllCards.Add(dateheadermodel);
                }

                missionPage.AllCards.Add(m);
            }

            missionPage.ResetVisible();
        }

        private string GetRelativeDateString(DateTime dt)
        {
            if(dt.Date < DateTime.Today)
            {
                if(dt.Date == DateTime.Today.AddDays(-1))
                {
                    return "Yesterday";
                }
                else
                {
                    return $"{(dt.Date - DateTime.Today).TotalDays * -1} Days Ago";
                }
            }

            if(dt.Date == DateTime.Today)
            {
                return "Today";
            }
            else if(dt.Date == DateTime.Today.AddDays(1))
            {
                return "Tomorrow";
            }
            else
            {
                return $"In {(dt.Date - DateTime.Today).TotalDays} Days"; 
            }
        }

        public void ScrollCardPageIntoView(ICardModel card)
        {
            var pg = Pages.FirstOrDefault(x => x.AllCards.Contains(card));
            int pos = Pages.IndexOf(pg);
            if (pos == -1) return;

            Position = pos;
        }

        public int GetCardPageIndex(ICardModel card)
        {
            var pg = Pages.FirstOrDefault(x => x.AllCards.Contains(card));
            return Pages.IndexOf(pg); // returns -1 if not found
        }

        private async Task OpenAchievementsAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Achievements.AchievementsPage(_db, GetTags()));
        }

        private async Task OpenDateRangePickerViewAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Shared.DateRangePickerPage(_db));
        }

        private async Task OpenPlannerViewAsync()
        {
            var mainQuest = Pages.First(p => p.Name == "Main Quest");
            var cards = mainQuest.AllCards.OfType<IActiveCardModel>().ToList();
            await Shell.Current.Navigation.PushAsync(new Points.Views.Planners.PlannerCreationPage(_db));
        }

        private async Task OpenSettingsAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Settings.SettingsPage(_db));
        }

        private async Task OpenReportsAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Reports.ReportPage(_db));
        }

        private async Task OpenShortcutDetailsAsync(ShortcutModel? shortcut)
        {
            if (shortcut is null)
                return;

            // 1) Build card options for the details page
            var optionsByType = BuildShortcutOptionsByType();

            // Work out the current/default target type from the shortcut's target card
            TargetCardType defaultType = TargetCardType.MainQuest;

            var matchingType = optionsByType
                .FirstOrDefault(kvp => kvp.Value.Any(o => o.CardId == shortcut.TargetCardId));

            if (!matchingType.Equals(default(KeyValuePair<TargetCardType, List<CardOption>>)))
                defaultType = matchingType.Key;

            // If the current target no longer exists, fall back to a sensible default
            var targetStillExists = optionsByType
                .SelectMany(kvp => kvp.Value)
                .Any(o => o.CardId == shortcut.TargetCardId);

            CardOption? fallbackTarget =
                (optionsByType.TryGetValue(TargetCardType.MainQuest, out var mq) ? mq.FirstOrDefault() : null)
                ?? optionsByType.SelectMany(kvp => kvp.Value).FirstOrDefault();

            if (!targetStillExists)
            {
                if (fallbackTarget == null || fallbackTarget.CardId <= 0)
                {
                    await Shell.Current.DisplayAlert(
                        "No targets",
                        "No valid cards are loaded to target. Create a card first, then edit the shortcut.",
                        "OK");
                    return;
                }

                shortcut.TargetCardId = fallbackTarget.CardId;
                defaultType = optionsByType
                    .FirstOrDefault(kvp => kvp.Value.Any(o => o.CardId == fallbackTarget.CardId))
                    .Key;
            }

            // 2) Load groups + shortcuts (for picker + validation / existing names)
            var groups = await _db.GetShortcutGroupsAsync();
            var shortcuts = await _db.GetDashboardShortcutsAsync();

            var existingGroupNames = groups
                .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                .OrderBy(g => g.ShortcutGroupOrder)
                .ThenBy(g => g.Name)
                .Select(g => g.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 3) Clone the model so cancel/editing does not mutate live dashboard state immediately
            var model = new ShortcutModel
            {
                ShortcutId = shortcut.ShortcutId,
                IconChar = shortcut.IconChar,
                TargetCardId = shortcut.TargetCardId,
                ShortcutOrder = shortcut.ShortcutOrder,
                ShortcutGroupId = shortcut.ShortcutGroupId,
                Group = shortcut.Group == null
                    ? new ShortcutGroupModel
                    {
                        ShortcutGroupId = 0,
                        Name = "",
                        Color = Colors.Black,
                        ShortcutGroupOrder = 0
                    }
                    : new ShortcutGroupModel
                    {
                        ShortcutGroupId = shortcut.Group.ShortcutGroupId,
                        Name = shortcut.Group.Name,
                        Color = shortcut.Group.Color,
                        ShortcutGroupOrder = shortcut.Group.ShortcutGroupOrder
                    }
            };

            // 4) Dashboard reload helper
            async Task ReloadDashboardAsync()
            {
                var updated = await _db.GetDashboardShortcutsAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var dashboard = Pages.First(p => p.Name == "Dashboard");
                    RebuildDashboardCells(dashboard, updated);
                });
            }

            // 5) Save callback
            Action<ShortcutModel> onSaved = saved =>
            {
                _ = Task.Run(async () =>
                {
                    if (saved.Group == null || string.IsNullOrWhiteSpace(saved.Group.Name))
                        throw new InvalidOperationException("Shortcut must have a Group with a Name before saving.");

                    var persistedGroup = await _db.UpsertShortcutGroupAsync(saved.Group);

                    saved.ShortcutGroupId = persistedGroup.ShortcutGroupId;
                    saved.Group = persistedGroup;

                    await _db.SaveShortcutAsync(saved);

                    await ReloadDashboardAsync();
                });
            };

            // 6) Delete callback
            Action<ShortcutModel> onDelete = deleted =>
            {
                _ = Task.Run(async () =>
                {
                    if (deleted.ShortcutId > 0)
                        await _db.DeleteShortcutAsync(deleted.ShortcutId);

                    await ReloadDashboardAsync();
                });
            };

            // 7) Navigate
            await Shell.Current.Navigation.PushAsync(
                new ShortcutDetailsPage(
                    model: model,
                    optionsByType: optionsByType,
                    existingGroups: groups,
                    onSaved: onSaved,
                    onDelete: onDelete,
                    defaultType: defaultType
                )
            );
        }




        public void FailMission(MissionCardModel model)
        {
            model.Fail(DateTime.Now);
            SortMissionCards();
        }

        public void DeleteMission(MissionCardModel model)
        {
            var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null)
                return;

            missionPage.AllCards.Remove(model);
            missionPage.VisibleCards.Remove(model);

            SortMissionCards();
            OnPropertyChanged(nameof(HasNegativeAvailableMission));
        }

        public async Task OpenExistingCardAsync(ICardModel model)
        {
            if (model == null) return;

            var page = Pages.FirstOrDefault(p => p.AllCards.Contains(model));
            if (page == null) return;

            await OpenDetailsForModelAsync(page, model);
        }


        #endregion

        public event Action? TickHappened;
        public void Tick()
        {
            Now = DateTime.Now;

            foreach (var page in Pages)
                foreach (var card in page.AllCards.OfType<MissionCardModel>())
                    card.NotifyTimeChanged();

            SortMissionCards();
            _ = UpdateAchievementsPerTickAsync();

            double total = 0;
            foreach (var page in Pages)
                foreach (var card in page.AllCards)
                    total += card.GetValue(RangeStart, RangeEnd);

            TopRightValue = total;

            OnPropertyChanged(nameof(RangeEnd));

            TickHappened?.Invoke();
        }

        private async Task UpdateAchievementsPerTickAsync()
        {
            if (!await _achievementTickGate.WaitAsync(0))
                return;

            try
            {
                var achievementsPage = Pages[Position];
                if (achievementsPage == null) return;

                if (achievementsPage.Name != "Challenges & Pinned Achievements") return;

                var achievementCards = achievementsPage.AllCards.OfType<AchievementCardModel>().ToList();
                if (achievementCards.Count == 0) return;

                var activeCards = Pages
                    .SelectMany(x => x.AllCards.Where(y => y is IActiveCardModel))
                    .Cast<IActiveCardModel>()
                    .ToList();

                foreach (var card in achievementCards)
                {
                    // Finalized deadline achievements are inert and frozen.
                    if (IsFinalizedDeadlineAchievement(card))
                    {
                        if (card.FrozenCurrentValue.HasValue)
                            card.CurrentValue = card.FrozenCurrentValue.Value;

                        card.NotifyTimeChanged();
                        continue;
                    }

                    var cardsForThisAchievement = activeCards
                        .Where(x => CardMatchesAchievementByTag(x, card))
                        .ToList();

                    var evaluations = cardsForThisAchievement
                        .SelectMany(x => x.TimeValueAchievementEvaluators ?? Enumerable.Empty<TimeValueAchievementEvaluator>())
                        .SelectMany(x => x.Evaluations ?? Enumerable.Empty<TimeValueAchievementEvaluation>())
                        .ToList();

                    // Live achievements still update dynamically.
                    card.UpdatePerTick(evaluations);

                    // Deadline achievements may transition during a tick.
                    if (card.CompletionType == AchievementCompletionType.Deadline)
                    {
                        var reevaluated = await _db.ReevaluateDeadlineAchievementAsync(card);

                        if (reevaluated != null)
                        {
                            ApplyAchievementRuntimeState(card, reevaluated);
                        }
                    }
                }
            }
            finally
            {
                _achievementTickGate.Release();
            }
        }

        private static bool IsFinalizedDeadlineAchievement(AchievementCardModel card)
        {
            return card != null
                && card.CompletionType == AchievementCompletionType.Deadline
                && card.FinalizedAt.HasValue;
        }

        private static bool CardMatchesAchievementByTag(IActiveCardModel activeCard, AchievementCardModel achievement)
        {
            if (activeCard == null || achievement == null)
                return false;

            if (string.IsNullOrWhiteSpace(achievement.Tags))
                return false;

            var achievementTag = achievement.Tags.Trim();

            return (activeCard.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(achievementTag, StringComparer.OrdinalIgnoreCase);
        }

        private static void ApplyAchievementRuntimeState(AchievementCardModel target, AchievementCardModel source)
        {
            if (target == null || source == null)
                return;

            // Core persisted fields that may change during runtime / finalization
            target.Status = source.Status;
            target.LastEarnedAt = source.LastEarnedAt;
            target.FinalizedAt = source.FinalizedAt;
            target.FrozenCurrentValue = source.FrozenCurrentValue;

            // These should remain consistent too in case they were changed/reloaded
            target.CreatedDate = source.CreatedDate;
            target.DeadlineStart = source.DeadlineStart;
            target.Deadline = source.Deadline;

            // Keep trophies in sync in case completion awarded one
            SyncTrophies(target, source);

            // Finalized deadline achievements must freeze at their frozen value
            if (source.CompletionType == AchievementCompletionType.Deadline &&
                source.FinalizedAt.HasValue)
            {
                target.CurrentValue = source.FrozenCurrentValue ?? source.CurrentValue;
            }
            else
            {
                target.CurrentValue = source.CurrentValue;
            }

            target.NotifyTimeChanged();
        }

        private static void SyncTrophies(AchievementCardModel target, AchievementCardModel source)
        {
            if (target == null || source == null)
                return;

            target.Trophies.Clear();

            foreach (var trophy in source.Trophies)
            {
                target.Trophies.Add(trophy);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        internal async Task IncrementFirstStep(ScCardModel model)
        {
            await _db.AddRepForStep(model.Steps[0].Id, DateTime.Now, model.Steps[0].StepValue);
        }

        internal async Task SaveMission(MissionCardModel model)
        {
            await _db.SaveCardModelAsync(model);
        }

        public async Task SaveBudget(BudgetCardModel b)
        {
            await _db.SaveCardModelAsync(b);
        }

        internal void DebugBeep()
        {
            _activeCardNotificationService.DebugBeep();
        }
    }

    public class HomePageModel : ObservableObject
    {
        public string IconChar { get; set; }
        public Color BackColor { get; set; } = Colors.Black;

        public int FontSize { get; set; } = 14;

        public string Name { get; }
        public ObservableCollection<ICardModel> AllCards { get; } = new();
        public ObservableCollection<ICardModel> VisibleCards { get; } = new();


        // ✅ NEW: only used by the Dashboard template
        public ObservableCollection<DashboardCellModel> DashboardCells { get; } = new();

        public bool IsDashboard => Name == "Dashboard";


        public HomePageModel(string name, IEnumerable<ICardModel> cards, string icon, int fontSize)
        {
            Name = name;
            FontSize = fontSize;
            foreach (var c in cards)
            {
                AllCards.Add(c);
                VisibleCards.Add(c);
            }
            IconChar = icon;
        }

        #region Methods

        public void ResetVisible()
        {
            VisibleCards.Clear();
            foreach (var c in AllCards)
                VisibleCards.Add(c);
        }

        public void ApplyFilter(Func<ICardModel, bool> predicate)
        {
            VisibleCards.Clear();
            foreach (var c in AllCards)
            {
                if (predicate(c))
                    VisibleCards.Add(c);
            }
        }

        public void AddCard(ICardModel card)
        {
            AllCards.Add(card);
            VisibleCards.Add(card);
        }

        public void SortCardsByLastActive()
        {
            var sorted = VisibleCards
                .OrderByDescending(x => ((IActiveCardModel)x).GetLastActiveTime())
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in sorted)
            {
                VisibleCards.Add(item);
            }
        }

        public void FilterCardsByTag(string choice)
        {
            var filtered = VisibleCards
                .Where(x => x.Tags.ToLower().Contains(choice.ToLower()))
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in filtered)
            {
                VisibleCards.Add(item);
            }
        }

        public void FilterCardsBySearchTerm(string choice)
        {
            var filtered = VisibleCards
                .Where(x =>
                    x.Tags.ToLower().Contains(choice.ToLower()) ||
                    x.Title.ToLower().Contains(choice.ToLower()))
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in filtered)
            {
                VisibleCards.Add(item);
            }
        }

        internal void RemoveCard(ICardModel card)
        {
            AllCards.Remove(card);
            VisibleCards.Remove(card);
        }



        #endregion
    }

    public sealed class DashboardCellModel : ObservableObject
    {
        public bool IsPlaceholder { get; set; }

        public ShortcutModel? Shortcut { get; set; }

        // Convenience bindings
        public string IconChar => Shortcut?.IconChar ?? "";
        public Color BackColor => Shortcut?.Group?.Color ?? Colors.Black;
    }
}
