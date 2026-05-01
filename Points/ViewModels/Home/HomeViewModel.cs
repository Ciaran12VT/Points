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
using Points.Services.Navigation;
using System.Windows.Input;
using Points.Services.Scheduling;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Home
{
    public class HomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly IClock _clock;
        private readonly HomePageStateCoordinator _pageState;
        private readonly HomeActivityInteractionCoordinator _activityInteraction;
        private readonly HomeCardLifecycleCoordinator _cardLifecycle;
        private readonly HomeCardWorkflowCoordinator _cardWorkflow;
        private readonly HomeDashboardShortcutWorkflowCoordinator _dashboardShortcuts;
        private readonly HomeGoalsPageCoordinator _goalsPage;
        private readonly HomeValueEntryCoordinator _valueEntries;
        private readonly HomeRuntimeTickCoordinator _runtimeTicks;
        private readonly HomeLoadCoordinator _homeLoader;
        private readonly HomeNavigationCoordinator _navigation;

        #region Commands
        public Command<IActiveCardModel> ActivateCardCommand { get; }
        public Command<ICardModel> OpenCardDetailsCommand { get; }
        public Command<MissionCardModel> CompleteMissionCommand { get; }
        public Command<ScCardModel> AddScFirstStepCommand { get; }

        public Command<ShortcutModel> ShortcutClickedCommand { get; }
        private void OnShortcutClicked(ShortcutModel? shortcut)
        {
            if (shortcut == null) return;

            using var suppression = BeginInteractionSuppression();
            SuppressTicksForShortcutNavigation();
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
        public Command SearchCardsCommand { get; }
        public Command SortByLastActiveCommand { get; }
        public Command OpenAchievementsCommand { get; }

        public Command OpenDateRangePickerViewCommand { get; }
        public Command OpenGoalViewCommand { get; }
        public Command OpenSettingsCommand { get; }
        public Command OpenReportsCommand { get; }
        public Command OpenLeaderboardCommand { get; }

        public Command ScrollToDashboardCommand { get; }

        public Command<ShortcutModel> OpenShortcutDetailsCommand { get; }

        public ICommand AddTrackerValueCommand { get; }
        public ICommand AddTrackerValueAtSelectedTimeCommand { get; }
        public Command<BudgetCardModel> SpendCommand { get; }
        public Command<BudgetCardModel> CashInCommand { get; }

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
        public DateTime _now = DateTime.MinValue;
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

        public IDisposable BeginInteractionSuppression()
        {
            return _runtimeTicks.BeginInteractionSuppression();
        }

        //A collection of the Carousel Pages (Main Quest, Mission, Budgets)
        public ObservableCollection<HomePageModel> Pages { get; } = new();

        //Returns a formatted date string for the app header
        public string HeaderDate
        {
            get
            {
                if(GlobalVariables.RangeStart.Date == GlobalVariables.RangeEnd.Date)
                {
                    return TimeDisplayFormatter.FormatLocal(GlobalVariables.RangeStart.Date, "MMM-dd-yyyy");
                }
                else
                {
                    return $"{TimeDisplayFormatter.FormatLocal(GlobalVariables.RangeStart.Date, "MMM-dd")} - {TimeDisplayFormatter.FormatLocal(GlobalVariables.RangeEnd.Date, "MMM-dd")}";
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
            get => _pageState.HasNegativeAvailableMission();
        }

        //The index of the currently displayed carousel page. Setting this here changes the page displayed.
        private int _position;
        public int Position
        {
            get => _position;
            set
            {
                if (_position == value) return;
                if (Pages.Count == 0 || value < 0 || value >= Pages.Count) return;
                _position = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPage));

                SetSelectedPageIcon();

                if (Pages[value].Name == "Goals") _ = ReloadGoalsAsync();

                if (Pages[value].IsDashboard) _ = ReloadDashboardAsync();
            }
        }

        public ICommand NavigationItemClickedCommand => new Command<HomePageModel>(OnNavigationItemClicked);

        private void SetSelectedPageIcon()
        {
            if (Pages.Count == 0 || Position < 0 || Position >= Pages.Count)
                return;

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
                SuppressTicksForPageNavigation();
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
            return _pageState.GetActiveCardModels();
        }

        public IReadOnlyList<IActiveCardModel> ActiveCardsForLocks => GetActiveCardModels();

        //A reference to the currenlty active card, if there is any
        private IActiveCardModel? _activeCard;

        private void SetActiveCard(IActiveCardModel? activeCard)
        {
            _activeCard = activeCard;
        }

        private void NotifyActiveCardChanged()
        {
            OnPropertyChanged(nameof(HasActiveCard));
            OnPropertyChanged(nameof(ActivePhaseName));
            OnPropertyChanged(nameof(ActivePhaseColor));
        }

        public Task? Initialization { get; private set; }


        #endregion

        public HomeViewModel(
            ICardReadService cardReader,
            ICardWriteService cardWriter,
            IDatabaseMaintenanceService databaseMaintenance,
            IDatabaseInitializationService databaseLifecycle,
            IReportService reports,
            IShortcutService shortcuts,
            IGoalService goals,
            ILockService locks,
            IActivityService activity,
            IAchievementService achievements,
            IBudgetService budgets,
            ITrackerService trackers,
            ITatCardService tats,
            INotificationLogService notificationLogs,
            ISettingsService settings,
            IUdmdService udmd,
            IPlannerService planner,
            IActiveCardNotificationService activeCardNotificationService,
            INotificationScheduleCoordinator scheduleCoordinator,
            ITimeZoneService timeZoneService,
            IAppNavigationService appNavigation,
            IAppDialogService dialogs,
            IPopupService popups,
            IAppPageService pageService,
            IClock clock)
        {
            _clock = clock;
            _pageState = new HomePageStateCoordinator(Pages, clock);
            _dashboardShortcuts = new HomeDashboardShortcutWorkflowCoordinator(
                shortcuts,
                appNavigation,
                dialogs,
                Pages);
            _goalsPage = new HomeGoalsPageCoordinator(cardReader, goals, clock, Pages);
            _valueEntries = new HomeValueEntryCoordinator(
                cardWriter,
                budgets,
                trackers,
                udmd,
                appNavigation,
                dialogs,
                popups,
                pageService,
                clock,
                timeZoneService,
                () => Now);
            _runtimeTicks = new HomeRuntimeTickCoordinator(
                achievements,
                clock,
                Pages,
                () => Position,
                () => RangeStart,
                () => RangeEnd,
                now => Now = now,
                value => TopRightValue = value,
                SortMissionCards,
                propertyName => OnPropertyChanged(propertyName),
                () => TickHappened?.Invoke());
            _activityInteraction = new HomeActivityInteractionCoordinator(
                activity,
                udmd,
                activeCardNotificationService,
                timeZoneService,
                appNavigation,
                dialogs,
                popups,
                pageService,
                clock,
                Pages,
                _pageState.GetActiveCardModels,
                SetActiveCard,
                NotifyActiveCardChanged);
            _cardLifecycle = new HomeCardLifecycleCoordinator(
                Pages,
                cardWriter,
                budgets,
                trackers,
                tats,
                clock,
                dialogs,
                _pageState.GetActiveCardModels,
                _activityInteraction.WireLongPress,
                SortMissionCards,
                propertyName => OnPropertyChanged(propertyName),
                ReloadDashboardAsync);
            _cardWorkflow = new HomeCardWorkflowCoordinator(
                locks,
                activity,
                achievements,
                udmd,
                clock,
                timeZoneService,
                appNavigation,
                dialogs,
                _cardLifecycle,
                _pageState.GetTags,
                _pageState.GetActiveCardModels);
            _homeLoader = new HomeLoadCoordinator(
                Pages,
                settings,
                cardReader,
                activity,
                scheduleCoordinator,
                clock,
                _pageState,
                _activityInteraction,
                _cardLifecycle,
                _dashboardShortcuts,
                _goalsPage,
                _runtimeTicks,
                () => Position,
                value => Position = value,
                SetSelectedPageIcon,
                SortMissionCards,
                propertyName => OnPropertyChanged(propertyName));
            _navigation = new HomeNavigationCoordinator(
                cardReader,
                cardWriter,
                databaseMaintenance,
                databaseLifecycle,
                reports,
                goals,
                achievements,
                notificationLogs,
                settings,
                planner,
                timeZoneService,
                appNavigation,
                dialogs,
                popups,
                clock,
                Pages,
                _pageState,
                _cardWorkflow,
                _dashboardShortcuts,
                _runtimeTicks,
                () => Initialization,
                task => Initialization = task,
                LoadAsync,
                value => RangeStart = value,
                value => RangeEnd = value,
                propertyName => OnPropertyChanged(propertyName));
            _now = _clock.LocalNow;

            // Commands
            ActivateCardCommand = new Command<IActiveCardModel>(RequestActivate);
            OpenCardDetailsCommand = new Command<ICardModel>(async model => await OpenExistingCardAsync(model));
            CompleteMissionCommand = new Command<MissionCardModel>(async model => await CompleteMissionAsync(model));
            AddScFirstStepCommand = new Command<ScCardModel>(async model => await _activityInteraction.AddScFirstStepAsync(model));
            ShortcutClickedCommand = new Command<ShortcutModel>(OnShortcutClicked);
            AddCardCommand = new Command(async () => await AddCardAsync());
            FilterPositiveCommand = new Command(ApplyPositiveFilter);
            FilterNegativeCommand = new Command(ApplyNegativeFilter);
            ClearFiltersCommand = new Command(ClearFilters);
            SearchCardsCommand = new Command(async () => await _navigation.SearchCardsByTextAsync());
            ScrollToActiveCardCommand = new Command(RequestScrollToActiveCard);
            SortByLastActiveCommand = new Command(SortCardsByLastActive);
            FilterByTagCommand = new Command(async () => await _navigation.FilterCardsByTagAsync());
            OpenAchievementsCommand = new Command(async () => await _navigation.OpenAchievementsAsync());
            OpenDateRangePickerViewCommand = new Command(async () => await _navigation.OpenDateRangePickerViewAsync());
            OpenGoalViewCommand = new Command(async () => await _navigation.OpenGoalViewAsync());
            OpenSettingsCommand = new Command(async () => await _navigation.OpenSettingsAsync());
            OpenReportsCommand = new Command(async () => await _navigation.OpenReportsAsync());
            OpenLeaderboardCommand = new Command(async () => await _navigation.OpenLeaderboardAsync());
            OpenShortcutDetailsCommand = new Command<ShortcutModel>(async shortcut => await _navigation.OpenShortcutDetailsAsync(shortcut));
            ScrollToDashboardCommand = new Command(() =>
            {
                SuppressTicksForPageNavigation();

                if (Pages.Count == 0)
                {
                    Position = 0;
                    return;
                }

                var dashboard = Pages.FirstOrDefault(p => p.IsDashboard) ?? Pages.First();
                Position = Pages.IndexOf(dashboard);
            });
            ToggleTopToolbarCommand = new Command(() =>
            {
                IsTopToolbarVisible = !IsTopToolbarVisible;
            });


            AddTrackerValueCommand = new Command<TrackerCardModel>(async card => await AddTrackerValueWithMetadataAsync(card));
            AddTrackerValueAtSelectedTimeCommand = new Command<TrackerCardModel>(async card => await AddTrackerValueAtSelectedTimeAsync(card));
            SpendCommand = new Command<BudgetCardModel>(async budget => await PromptAndRecordBudgetTransactionAsync(budget, BudgetTransactionType.Spend));
            CashInCommand = new Command<BudgetCardModel>(async budget => await PromptAndRecordBudgetTransactionAsync(budget, BudgetTransactionType.CashIn));

            // kick off async load without awaiting
            Initialization = LoadAsync();
        }

        private async Task AddTrackerValueWithMetadataAsync(TrackerCardModel? card)
        {
            await _valueEntries.AddTrackerValueWithMetadataAsync(card);
        }

        private async Task AddTrackerValueAtSelectedTimeAsync(TrackerCardModel? card)
        {
            await _valueEntries.AddTrackerValueAtSelectedTimeAsync(card);
        }

        private async Task PromptAndRecordBudgetTransactionAsync(BudgetCardModel? budget, BudgetTransactionType type)
        {
            await _valueEntries.PromptAndRecordBudgetTransactionAsync(budget, type);
        }

        public async Task LoadAsync()
        {
            await _homeLoader.LoadAsync();
        }

        private async Task ReloadGoalsAsync()
        {
            await _goalsPage.ReloadGoalsAsync();
        }

        private async Task ReloadDashboardAsync()
        {
            await _dashboardShortcuts.ReloadDashboardAsync();
        }

        #region Add card pipeline

        /// <summary>
        /// UI entry: Add button.
        /// Delegates creation and details navigation to the card workflow coordinator.
        /// </summary>
        private async Task AddCardAsync()
        {
            var page = CurrentPage;

            if (page.Name == "Dashboard")
            {
                await AddDashboardShortcutAsync();
                return;
            }

            await _cardWorkflow.AddCardFlowAsync(model: null, targetPage: page, openDetails: true);
        }

        private async Task AddDashboardShortcutAsync()
        {
            await _dashboardShortcuts.AddDashboardShortcutAsync();
        }

        #endregion

        #region Methods (existing behavior preserved)

        public async void RequestActivate(IActiveCardModel card)
        {
            await _activityInteraction.RequestActivateAsync(card);
        }

        public async void RequestActivate(IActiveCardModel card, DateTime? nowUtc = null)
        {
            await _activityInteraction.RequestActivateAsync(card, nowUtc);
        }

        private async Task CompleteMissionAsync(MissionCardModel? model)
        {
            await _cardLifecycle.CompleteMissionAsync(model);
        }


        private void ApplyPositiveFilter()
        {
            _pageState.ApplyPositiveFilter(CurrentPage);
        }

        private void ApplyNegativeFilter()
        {
            _pageState.ApplyNegativeFilter(CurrentPage);
        }

        private void ClearFilters()
        {
            _pageState.ClearFilters(CurrentPage);
        }

        private void SortCardsByLastActive()
        {
            _pageState.SortCardsByLastActive(CurrentPage);
        }

        private void RequestScrollToActiveCard()
        {
            if (_activeCard == null)
                return;

            ScrollToCardRequested?.Invoke((IActiveCardModel)_activeCard);
        }

        private void SortMissionCards()
        {
            _pageState.SortMissionCards();
        }

        public void ScrollCardPageIntoView(ICardModel card)
        {
            var pos = _pageState.GetCardPageIndex(card);
            if (pos == -1) return;

            SuppressTicksForPageNavigation();
            Position = pos;
        }

        public int GetCardPageIndex(ICardModel card)
        {
            return _pageState.GetCardPageIndex(card);
        }

        public async Task OpenExistingCardAsync(ICardModel model)
        {
            await _navigation.OpenExistingCardAsync(model);
        }


        #endregion

        public event Action? TickHappened;
        public void Tick()
        {
            _runtimeTicks.Tick();
        }

        private void SuppressTicksForPageNavigation()
        {
            _runtimeTicks.SuppressTicksForPageNavigation();
        }

        private void SuppressTicksForShortcutNavigation()
        {
            _runtimeTicks.SuppressTicksForShortcutNavigation();
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public async Task SaveBudget(BudgetCardModel b)
        {
            await _valueEntries.SaveBudgetAsync(b);
        }

        public async Task RecordBudgetTransactionAsync(BudgetCardModel budget, BudgetTransactionType type, double amount)
        {
            await _valueEntries.RecordBudgetTransactionAsync(budget, type, amount);
        }

    }

}
