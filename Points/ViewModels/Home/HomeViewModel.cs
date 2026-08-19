using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using Points.Models;
using Points.Global;
using Points.Services;
using Points.Services.Backup;
using Points.Services.Navigation;
using Points.Services.MissionSharing;
using System.Windows.Input;
using Points.Services.Scheduling;
using Points.Services.Persistence;
using Points.Services.Premium;
using Points.Services.Time;
using Points.Services.Diagnostics;
using Points.Services.Watch;
using Points.Views.Premium;

namespace Points.ViewModels.Home
{
    public class HomeViewModel : INotifyPropertyChanged, IAsyncDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private const string PremiumPromptLaunchCountKey = "PremiumPromptLaunchCount";
        private const int PremiumPromptLaunchInterval = 10;
        private static readonly TimeSpan MissedGracePeriod = TimeSpan.FromMinutes(15);

        private readonly IClock _clock;
        private readonly IActivityService _activity;
        private readonly IWatchSnapshotPublishService _watchSnapshots;
        private readonly IActiveCardChangeNotifier _activeCardChanges;
        private readonly IActiveCardNotificationNavigationService _activeCardNotificationNavigation;
        private readonly INotificationLogService _notificationLogs;
        private readonly ICardWriteService _cardWriter;
        private readonly IAppDialogService _dialogs;
        private readonly IPopupService _popups;
        private readonly IPremiumSubscriptionService _premiumSubscriptions;
        private readonly IHardModePenaltyService _hardModePenalties;
        private readonly HomePageStateCoordinator _pageState;
        private readonly HomeActivityInteractionCoordinator _activityInteraction;
        private readonly HomeCardLifecycleCoordinator _cardLifecycle;
        private readonly HomeCardWorkflowCoordinator _cardWorkflow;
        private readonly HomeDashboardShortcutWorkflowCoordinator _dashboardShortcuts;
        private readonly HomeGoalsPageCoordinator _goalsPage;
        private readonly HomeValueEntryCoordinator _valueEntries;
        private readonly HomeRuntimeTickCoordinator _runtimeTicks;
        private readonly HomeLoadCoordinator _homeLoader;
        private readonly HomeRefreshCoordinator _refreshes;
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

            var targetCard = FindCardById(shortcut.TargetCardId);
            if (targetCard == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Dashboard shortcut target not found. ShortcutId={shortcut.ShortcutId}, TargetCardId={shortcut.TargetCardId}");
                return;
            }

            using var suppression = BeginInteractionSuppression();
            SuppressTicksForShortcutNavigation();

            if (ScrollToCardModelRequested != null)
            {
                ScrollToCardModelRequested(targetCard);
                return;
            }

            ScrollToAnyCardByIdRequested?.Invoke(shortcut.TargetCardId);
        }

        public Command AddCardCommand { get; }
        public Command ScrollToActiveCardCommand { get; }
        public Command FilterByTagCommand { get; }

        public Action<IActiveCardModel>? ScrollToCardRequested;

        public Action<ICardModel>? ScrollToCardModelRequested;

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
        public Command OpenPremiumUpgradeCommand { get; }
        public Command OpenMissedNotificationsLogCommand { get; }

        public Command ScrollToDashboardCommand { get; }
        public Command<HomePageModel> NavigationItemClickedCommand { get; }

        public Command<ShortcutModel> OpenShortcutDetailsCommand { get; }
        public Command ToggleOrderModeCommand { get; }
        public Command<ICardModel> MoveCardUpCommand { get; }
        public Command<ICardModel> MoveCardDownCommand { get; }

        public ICommand AddTrackerValueCommand { get; }
        public ICommand AddTrackerValueAtSelectedTimeCommand { get; }
        public Command<BudgetCardModel> SpendCommand { get; }
        public Command<BudgetCardModel> CashInCommand { get; }

        #endregion

        #region Fields

        private bool _hasRecordedPremiumPromptLaunch;
        private bool _isPremiumPromptShowing;
        private int _missedNotificationCount;
        private int _activeCardNotificationNavigationInProgress;
        private int _disposeStarted;

        public int MissedNotificationCount
        {
            get => _missedNotificationCount;
            private set
            {
                if (_missedNotificationCount == value)
                    return;

                _missedNotificationCount = value;
                OnPropertyChanged(nameof(MissedNotificationCount));
                OnPropertyChanged(nameof(HasMissedNotifications));
                OnPropertyChanged(nameof(MissedNotificationBadgeText));
            }
        }

        public bool HasMissedNotifications => MissedNotificationCount > 0;
        public string MissedNotificationBadgeText => MissedNotificationCount.ToString(CultureInfo.InvariantCulture);
        public string MissedNotificationBadgeColor => NotificationLogStatusColors.Missed;

        private bool _isPremiumBannerVisible = true;
        public bool IsPremiumBannerVisible
        {
            get => _isPremiumBannerVisible;
            set
            {
                if (_isPremiumBannerVisible == value)
                    return;

                _isPremiumBannerVisible = value;
                OnPropertyChanged(nameof(IsPremiumBannerVisible));
            }
        }

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

        private bool _isOrderMode;
        public bool IsOrderMode
        {
            get => _isOrderMode;
            set
            {
                if (_isOrderMode == value)
                    return;

                _isOrderMode = value;
                OnPropertyChanged(nameof(IsOrderMode));
            }
        }



        //Used to check if there is an card currenty active
        public bool HasActiveCard => _activeCard is not null;

        // Returns the carousel page that is currently displayed.
        private HomePageModel? CurrentPage => SelectedPage;

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

        public string ActiveMultiplierCode => MultiplierRuntimeState.ActiveCode;

        public bool HasActiveMultiplier => MultiplierRuntimeState.HasActiveMultiplier;

        private HomePageModel? _selectedPage;
        private bool _isReconcilingPages;
        private bool _selectionRestoreNeeded;
        private int _selectionRestoreQueued;

        public HomePageModel? SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (_isReconcilingPages)
                {
                    _selectionRestoreNeeded = true;
                    return;
                }

                // CarouselView can transiently write null/stale items while its source is changing.
                if (value == null && Pages.Count > 0)
                {
                    QueueSelectedPageRestore();
                    return;
                }

                if (value != null && !Pages.Contains(value))
                {
                    QueueSelectedPageRestore();
                    return;
                }

                SetSelectedPageCore(value, refreshPage: true, forceNotify: false);
            }
        }

        private void QueueSelectedPageRestore()
        {
            if (Interlocked.Exchange(ref _selectionRestoreQueued, 1) == 1)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Interlocked.Exchange(ref _selectionRestoreQueued, 0);

                if (_isReconcilingPages)
                    return;

                OnPropertyChanged(nameof(SelectedPage));
                OnPropertyChanged(nameof(Position));
            });
        }

        // Compatibility bridge for code that still consumes the selected pane as an index.
        public int Position
        {
            get
            {
                var index = SelectedPage == null ? -1 : Pages.IndexOf(SelectedPage);
                return index >= 0 ? index : 0;
            }
            set
            {
                if (value < 0 || value >= Pages.Count)
                    return;

                SelectedPage = Pages[value];
            }
        }

        private void BeginPageReconciliation()
        {
            _isReconcilingPages = true;
            _selectionRestoreNeeded = false;
        }

        private void CompletePageReconciliation(
            HomePageModel? selectedPage,
            bool layoutChanged)
        {
            var forceNotify = layoutChanged || _selectionRestoreNeeded;
            _isReconcilingPages = false;
            _selectionRestoreNeeded = false;
            SetSelectedPageCore(selectedPage, refreshPage: false, forceNotify: forceNotify);
        }

        private void SetSelectedPageCore(
            HomePageModel? page,
            bool refreshPage,
            bool forceNotify)
        {
            var changed = !ReferenceEquals(_selectedPage, page);
            if (!changed && !forceNotify)
                return;

            _selectedPage = page;
            OnPropertyChanged(nameof(SelectedPage));
            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(CurrentPage));
            SetSelectedPageIcon();

            if (!changed || !refreshPage || page == null)
                return;

            if (page.Name == "Goals")
                TaskSupervisor.Forget(ReloadGoalsAsync(), "Reload Goals after pane selection");

            if (page.IsDashboard)
                TaskSupervisor.Forget(ReloadDashboardAsync(), "Reload Dashboard after pane selection");
        }

        private void SetSelectedPageIcon()
        {
            if (Pages.Count == 0 || Position < 0 || Position >= Pages.Count)
                return;

            foreach (var page in Pages)
            {
                var color = ReferenceEquals(page, SelectedPage) ? Colors.Green : Colors.Black;
                if (page.BackColor == color)
                    continue;

                page.BackColor = color;
                page.RaisePropertyChanged(nameof(HomePageModel.BackColor));
            }
        }

        private void OnNavigationItemClicked(HomePageModel hpm)
        {
            JumpToPage(hpm);
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
            IHardModePenaltyService hardModePenalties,
            IUserMultiplierService userMultipliers,
            IUdmdService udmd,
            IPlannerService planner,
            IActiveCardNotificationService activeCardNotificationService,
            INotificationScheduleCoordinator scheduleCoordinator,
            IMissionShareService missionShares,
            ITimeZoneService timeZoneService,
            IAppNavigationService appNavigation,
            IAppDialogService dialogs,
            IPopupService popups,
            IAppPageService pageService,
            IBackupFileStorageService backupFileStorage,
            IScheduledBackupConfigStore scheduledBackupConfigStore,
            IScheduledBackupLogStore scheduledBackupLogStore,
            IGoogleDriveBackupConnector googleDriveBackupConnector,
            IScheduledBackupWorkScheduler scheduledBackupWorkScheduler,
            IPremiumSubscriptionService premiumSubscriptions,
            IWatchSnapshotPublishService watchSnapshots,
            IWatchShortcutSettingsService watchShortcuts,
            IActiveCardChangeNotifier activeCardChanges,
            IActiveCardNotificationNavigationService activeCardNotificationNavigation,
            IClock clock)
        {
            _clock = clock;
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _watchSnapshots = watchSnapshots ?? throw new ArgumentNullException(nameof(watchSnapshots));
            _activeCardChanges = activeCardChanges ?? throw new ArgumentNullException(nameof(activeCardChanges));
            _activeCardNotificationNavigation = activeCardNotificationNavigation ?? throw new ArgumentNullException(nameof(activeCardNotificationNavigation));
            _notificationLogs = notificationLogs ?? throw new ArgumentNullException(nameof(notificationLogs));
            _cardWriter = cardWriter ?? throw new ArgumentNullException(nameof(cardWriter));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _popups = popups ?? throw new ArgumentNullException(nameof(popups));
            _premiumSubscriptions = premiumSubscriptions ?? throw new ArgumentNullException(nameof(premiumSubscriptions));
            _hardModePenalties = hardModePenalties ?? throw new ArgumentNullException(nameof(hardModePenalties));
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
                () =>
                {
                    TickHappened?.Invoke();
                    _watchSnapshots.RequestPublishAsync().Forget("Watch snapshot tick publish");
                },
                hardModePenalties);
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
                NotifyActiveCardChanged,
                hardModePenalties);
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
                ReloadDashboardAsync,
                missionShares);
            _cardWorkflow = new HomeCardWorkflowCoordinator(
                locks,
                activity,
                achievements,
                udmd,
                clock,
                timeZoneService,
                missionShares,
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
                userMultipliers,
                () => SelectedPage,
                () => Position,
                BeginPageReconciliation,
                CompletePageReconciliation,
                propertyName => OnPropertyChanged(propertyName));
            _refreshes = new HomeRefreshCoordinator(
                ExecuteFullRefreshAsync,
                ExecuteActiveRefreshAsync);
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
                hardModePenalties,
                userMultipliers,
                planner,
                timeZoneService,
                appNavigation,
                dialogs,
                popups,
                clock,
                backupFileStorage,
                scheduledBackupConfigStore,
                scheduledBackupLogStore,
                googleDriveBackupConnector,
                scheduledBackupWorkScheduler,
                premiumSubscriptions,
                _watchSnapshots,
                watchShortcuts,
                Pages,
                _pageState,
                _cardWorkflow,
                _dashboardShortcuts,
                () => Initialization,
                RefreshForDateRangeAsync,
                () => RangeStart,
                () => RangeEnd,
                value => RangeStart = value,
                value => RangeEnd = value,
                propertyName => OnPropertyChanged(propertyName));
            _now = _clock.LocalNow;

            // Commands
            ActivateCardCommand = new Command<IActiveCardModel>(RequestActivate);
            OpenCardDetailsCommand = new Command<ICardModel>(async model => await OpenExistingCardAsync(model));
            CompleteMissionCommand = new Command<MissionCardModel>(async model => await CompleteMissionAsync(model));
            AddScFirstStepCommand = new Command<ScCardModel>(async model =>
            {
                await _activityInteraction.AddScFirstStepAsync(model);
                await _watchSnapshots.RequestPublishAsync(force: true);
            });
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
            OpenPremiumUpgradeCommand = new Command(async () => await ShowPremiumUpgradeAsync());
            OpenMissedNotificationsLogCommand = new Command(async () => await _navigation.OpenMissedNotificationsLogAsync());
            NavigationItemClickedCommand = new Command<HomePageModel>(OnNavigationItemClicked);
            OpenShortcutDetailsCommand = new Command<ShortcutModel>(async shortcut => await _navigation.OpenShortcutDetailsAsync(shortcut));
            ScrollToDashboardCommand = new Command(() =>
            {
                if (Pages.Count == 0)
                {
                    Position = 0;
                    return;
                }

                var dashboard = Pages.FirstOrDefault(p => p.IsDashboard) ?? Pages.First();
                JumpToPage(dashboard);
            });
            ToggleTopToolbarCommand = new Command(() =>
            {
                IsTopToolbarVisible = !IsTopToolbarVisible;
            });
            ToggleOrderModeCommand = new Command(() =>
            {
                IsOrderMode = !IsOrderMode;
            });
            MoveCardUpCommand = new Command<ICardModel>(async card => await MoveCardByOffsetAsync(card, -1));
            MoveCardDownCommand = new Command<ICardModel>(async card => await MoveCardByOffsetAsync(card, 1));


            AddTrackerValueCommand = new Command<TrackerCardModel>(async card => await AddTrackerValueWithMetadataAsync(card));
            AddTrackerValueAtSelectedTimeCommand = new Command<TrackerCardModel>(async card => await AddTrackerValueAtSelectedTimeAsync(card));
            SpendCommand = new Command<BudgetCardModel>(async budget => await PromptAndRecordBudgetTransactionAsync(budget, BudgetTransactionType.Spend));
            CashInCommand = new Command<BudgetCardModel>(async budget => await PromptAndRecordBudgetTransactionAsync(budget, BudgetTransactionType.CashIn));

            _activeCardChanges.ActiveCardChanged += OnExternalActiveCardChanged;
            _activeCardChanges.CardDataChanged += OnExternalCardDataChanged;
            _activeCardNotificationNavigation.NavigationRequested += OnActiveCardNotificationNavigationRequested;

            // Subscribe before starting the load so startup notifications are coalesced, not lost.
            Initialization = RequestFullRefreshAsync(HomeFullRefreshReason.Initial, RangeStart, RangeEnd);
        }

        public void ProcessPendingActiveCardNotificationNavigation()
        {
            var pendingCardId = _activeCardNotificationNavigation.PendingCardId;
            if (pendingCardId.HasValue)
                QueueActiveCardNotificationNavigation(pendingCardId.Value);
        }

        private void OnActiveCardNotificationNavigationRequested(
            object? sender,
            ActiveCardNotificationNavigationRequestedEventArgs e)
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return;

            QueueActiveCardNotificationNavigation(e.CardId);
        }

        private void QueueActiveCardNotificationNavigation(long cardId)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TaskSupervisor.Forget(
                    NavigateToActiveCardFromNotificationAsync(cardId),
                    "Open active card from notification");
            });
        }

        private async Task NavigateToActiveCardFromNotificationAsync(long cardId)
        {
            if (cardId <= 0)
                return;

            if (Interlocked.Exchange(ref _activeCardNotificationNavigationInProgress, 1) == 1)
                return;

            try
            {
                var initialization = Initialization;
                if (initialization != null)
                    await initialization;

                await _refreshes.WaitThroughCurrentVersionAsync();

                if (ScrollToCardModelRequested == null && ScrollToAnyCardByIdRequested == null)
                    return;

                try
                {
                    await _navigation.ReturnHomeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }

                var targetCard = FindCardById(cardId);
                if (targetCard == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Active card notification target not found. TargetCardId={cardId}");
                    _activeCardNotificationNavigation.ClearPending(cardId);
                    return;
                }

                SuppressTicksForShortcutNavigation();

                if (ScrollToCardModelRequested != null)
                    ScrollToCardModelRequested(targetCard);
                else
                    ScrollToAnyCardByIdRequested?.Invoke(cardId);

                _activeCardNotificationNavigation.ClearPending(cardId);
            }
            finally
            {
                Interlocked.Exchange(ref _activeCardNotificationNavigationInProgress, 0);
            }
        }

        private void OnExternalActiveCardChanged(object? sender, ActiveCardChangedEventArgs e)
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return;

            TaskSupervisor.Forget(
                _refreshes.RequestActiveRefreshAsync(e.ToggleResult),
                "Refresh Home after external active-card change");
        }

        private async Task ExecuteActiveRefreshAsync(
            HomeActiveRefreshContext context,
            IReadOnlyList<ToggleActivityModelResult> toggleResults,
            bool requiresDatabaseRead,
            CancellationToken cancellationToken)
        {
            var openActivity = requiresDatabaseRead || toggleResults.Count == 0
                ? await _activity.GetCurrentActiveActivityAsync()
                : null;
            cancellationToken.ThrowIfCancellationRequested();

            if (!context.IsCurrent)
                return;

            var applied = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                applied = context.TryCommit(() =>
                {
                    foreach (var toggleResult in toggleResults)
                        _activityInteraction.ApplyExternalToggleResult(toggleResult);

                    if (requiresDatabaseRead || toggleResults.Count == 0)
                        _activityInteraction.RestoreActiveCardFromOpenActivity(openActivity);
                });
            });

            if (!applied || !context.IsCurrent)
                return;

            await MainThread.InvokeOnMainThreadAsync(
                _runtimeTicks.RunImmediateWithoutCompletionNotificationAsync);
        }

        private void OnExternalCardDataChanged(object? sender, CardDataChangedEventArgs e)
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return;

            TaskSupervisor.Forget(
                RequestFullRefreshAsync(HomeFullRefreshReason.ExternalCardData, RangeStart, RangeEnd),
                "Refresh Home after external card-data change");
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
            await _watchSnapshots.RequestPublishAsync(force: true);
        }

        public async Task LoadAsync()
        {
            await RequestFullRefreshAsync(HomeFullRefreshReason.Explicit, RangeStart, RangeEnd);
        }

        private Task RequestFullRefreshAsync(
            HomeFullRefreshReason reason,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            return _refreshes.RequestFullRefreshAsync(reason, rangeStart, rangeEnd);
        }

        private Task RefreshForDateRangeAsync(DateTime rangeStart, DateTime rangeEnd)
        {
            return RequestFullRefreshAsync(HomeFullRefreshReason.DateRangeChanged, rangeStart, rangeEnd);
        }

        private async Task ExecuteFullRefreshAsync(
            HomeFullRefreshContext context,
            CancellationToken cancellationToken)
        {
            var committed = await _homeLoader.LoadAsync(
                context.RangeStart,
                context.RangeEnd,
                context.TryCommit,
                cancellationToken);

            if (!committed || !context.IsCurrent)
                return;

            try
            {
                await _hardModePenalties.ReconcileAsync(_clock.UtcNow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hard-mode reconciliation after Home refresh failed: {ex}");
            }
            cancellationToken.ThrowIfCancellationRequested();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!context.IsCurrent)
                    return;

                try
                {
                    await RefreshPremiumStateAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Premium refresh after Home load failed: {ex}");
                }

                try
                {
                    await _runtimeTicks.RunImmediateWithoutCompletionNotificationAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Immediate tick after Home load failed: {ex}");
                }
            });

            if (!context.IsCurrent)
                return;

            var nonWatchReasons = context.Reasons & ~HomeFullRefreshReason.ExternalCardData;
            if (nonWatchReasons != HomeFullRefreshReason.None)
            {
                TaskSupervisor.Forget(
                    _watchSnapshots.RequestPublishAsync(force: true),
                    "Publish watch snapshot after Home refresh");
            }
        }

        public async Task RefreshMissedNotificationBadgeAsync()
        {
            await _notificationLogs.MarkOverdueNotificationLogsMissedAsync(_clock.UtcNow, MissedGracePeriod);
            MissedNotificationCount = await _notificationLogs.GetNotificationLogCountAsync(NotificationLogFilter.Missed);
        }

        public async Task HandleHomeOpenedForPremiumPromptAsync()
        {
            if (_hasRecordedPremiumPromptLaunch)
                return;

            _hasRecordedPremiumPromptLaunch = true;
            await RefreshPremiumStateAsync();

            if (!IsPremiumBannerVisible)
                return;

            var launchCount = Preferences.Get(PremiumPromptLaunchCountKey, 0) + 1;
            Preferences.Set(PremiumPromptLaunchCountKey, launchCount);

            if (launchCount % PremiumPromptLaunchInterval == 0)
                await ShowPremiumUpgradeAsync();
        }

        private async Task RefreshPremiumStateAsync()
        {
            IsPremiumBannerVisible = !await _premiumSubscriptions.HasPremiumAsync();
        }

        private async Task ShowPremiumUpgradeAsync()
        {
            if (_isPremiumPromptShowing)
                return;

            _isPremiumPromptShowing = true;

            try
            {
                var result = await _popups.ShowPopupAsync(new PremiumUpgradePopup());

                if (result is PremiumUpgradePopupResult.Upgrade)
                {
                    await _dialogs.DisplayAlertAsync(
                        "Premium",
                        "Premium subscriptions are not available yet.",
                        "OK");
                }
            }
            finally
            {
                _isPremiumPromptShowing = false;
                await RefreshPremiumStateAsync();
            }
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

            if (page == null)
                return;

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
            await _watchSnapshots.RequestPublishAsync(force: true);
        }

        public async void RequestActivate(IActiveCardModel card, DateTime? nowUtc = null)
        {
            await _activityInteraction.RequestActivateAsync(card, nowUtc);
            await _watchSnapshots.RequestPublishAsync(force: true);
        }

        private async Task CompleteMissionAsync(MissionCardModel? model)
        {
            await _cardLifecycle.CompleteMissionAsync(model);
        }


        private void ApplyPositiveFilter()
        {
            if (CurrentPage != null)
                _pageState.ApplyPositiveFilter(CurrentPage);
        }

        private void ApplyNegativeFilter()
        {
            if (CurrentPage != null)
                _pageState.ApplyNegativeFilter(CurrentPage);
        }

        private void ClearFilters()
        {
            if (CurrentPage != null)
                _pageState.ClearFilters(CurrentPage);
        }

        private void SortCardsByLastActive()
        {
            if (CurrentPage != null)
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

            JumpToPage(pos);
        }

        private void JumpToPage(HomePageModel? page)
        {
            if (page == null)
                return;

            var position = Pages.IndexOf(page);
            if (position > -1)
                JumpToPage(position);
        }

        private void JumpToPage(int position)
        {
            if (Pages.Count == 0 || position < 0 || position >= Pages.Count)
                return;

            SuppressTicksForPageNavigation();
            SelectedPage = Pages[position];
        }

        public int GetCardPageIndex(ICardModel card)
        {
            return _pageState.GetCardPageIndex(card);
        }

        private ICardModel? FindCardById(long cardId)
        {
            if (cardId <= 0)
                return null;

            foreach (var page in Pages)
            {
                var card = page.AllCards.FirstOrDefault(c => c.CardID == cardId);
                if (card != null)
                    return card;
            }

            return null;
        }

        public async Task OpenExistingCardAsync(ICardModel model)
        {
            await _navigation.OpenExistingCardAsync(model);
        }

        public async Task ReorderCardsAsync(ICardModel? dragged, ICardModel? target)
        {
            if (dragged == null || target == null)
                return;

            var page = _pageState.FindPageContaining(dragged);
            if (page == null || !page.IsCardReorderEnabled)
                return;

            if (!ReferenceEquals(page, _pageState.FindPageContaining(target)))
                return;

            if (!page.MoveCard(dragged, target))
                return;

            var persistedCards = page.AllCards
                .Where(c => c.CardID > 0)
                .ToList();

            if (persistedCards.Count > 0)
                await _cardWriter.SaveCardDisplayOrderAsync(persistedCards);
        }

        private async Task MoveCardByOffsetAsync(ICardModel? card, int offset)
        {
            if (card == null)
                return;

            var page = _pageState.FindPageContaining(card);
            if (page == null || !page.IsCardReorderEnabled)
                return;

            if (!page.MoveCardByOffset(card, offset))
                return;

            var persistedCards = page.AllCards
                .Where(c => c.CardID > 0)
                .ToList();

            if (persistedCards.Count > 0)
                await _cardWriter.SaveCardDisplayOrderAsync(persistedCards);
        }


        #endregion

        public event Action? TickHappened;
        public Task TickAsync()
        {
            return _runtimeTicks.TickAsync();
        }

        public void Tick()
        {
            _runtimeTicks.Tick();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) == 1)
                return;

            _activeCardChanges.ActiveCardChanged -= OnExternalActiveCardChanged;
            _activeCardChanges.CardDataChanged -= OnExternalCardDataChanged;
            _activeCardNotificationNavigation.NavigationRequested -= OnActiveCardNotificationNavigationRequested;

            ScrollToCardRequested = null;
            ScrollToCardModelRequested = null;
            ScrollToAnyCardByIdRequested = null;

            await _refreshes.DisposeAsync();
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
            await _watchSnapshots.RequestPublishAsync(force: true);
        }

        public async Task RecordBudgetTransactionAsync(BudgetCardModel budget, BudgetTransactionType type, double amount)
        {
            await _valueEntries.RecordBudgetTransactionAsync(budget, type, amount);
            await _watchSnapshots.RequestPublishAsync(force: true);
        }

    }

}
