using Points.Helpers;
using Points.Interfaces;
using Points.Models;
using Points.Services.Backup;
using Points.Services.Diagnostics;
using Points.ViewModels.Home;

namespace Points.Views.Home;

public partial class HomePage : ContentPage
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private PeriodicTimer? _scheduledBackupTimer;
    private CancellationTokenSource? _scheduledBackupCts;
    readonly IAudioFeedback _audio;
    private readonly IScheduledBackupRunner _scheduledBackupRunner;
    int _lastPos = -1;
    private readonly Dictionary<HomePageModel, CollectionView> _cardsListsByPage = new();
    private static readonly TimeSpan ScrollInteractionSettleDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan PaneMaterializeTimeout = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan PaneMaterializePollDelay = TimeSpan.FromMilliseconds(16);
    private CancellationTokenSource? _cardsScrollSettleCts;
    private IDisposable? _cardsScrollSuppressionHandle;
    private CancellationTokenSource? _carouselScrollSettleCts;
    private IDisposable? _carouselScrollSuppressionHandle;
    private bool _wasParented;
    private int _disposeStarted;


    public HomePage(HomeViewModel vm, IAudioFeedback audio, IScheduledBackupRunner scheduledBackupRunner)
	{
		InitializeComponent();
        BindingContext = vm;
        _audio = audio;
        _scheduledBackupRunner = scheduledBackupRunner ?? throw new ArgumentNullException(nameof(scheduledBackupRunner));

        vm.ScrollToCardRequested = ScrollToCard;
        vm.ScrollToCardModelRequested = ScrollToCardModel;
        vm.ScrollToAnyCardByIdRequested = ScrollToCardById;
        vm.ProcessPendingActiveCardNotificationNavigation();
    }

    private void CardsList_Loaded(object sender, EventArgs e)
    {
        if (sender is not CollectionView cv) return;
        if (cv.BindingContext is not HomePageModel page) return;

        _cardsListsByPage[page] = cv;
    }

    private void CardsList_Unloaded(object sender, EventArgs e)
    {
        if (sender is not CollectionView cv) return;
        if (cv.BindingContext is not HomePageModel page) return;

        if (_cardsListsByPage.TryGetValue(page, out var existing) && ReferenceEquals(existing, cv))
            _cardsListsByPage.Remove(page);
    }

    private async void ScrollToCard(IActiveCardModel card)
    {
        if (BindingContext is not HomeViewModel vm) return;
        await ScrollToCardInternal(vm, card);
    }

    private async void ScrollToCardModel(ICardModel card)
    {
        if (BindingContext is not HomeViewModel vm) return;
        await ScrollToCardInternal(vm, card);
    }

    private async void ScrollToCardById(long cardId)
    {
        if (BindingContext is not HomeViewModel vm) return;
        if (cardId <= 0) return;

        // Find the target card anywhere in the loaded pages
        var targetCard = vm.Pages
            .SelectMany(p => p.AllCards)
            .FirstOrDefault(c => c.CardID == cardId);

        if (targetCard == null)
            return;

        await ScrollToCardInternal(vm, targetCard);
    }

    private async Task ScrollToCardInternal(HomeViewModel vm, ICardModel card)
    {
        using var suppression = vm.BeginInteractionSuppression();

        var targetPage = vm.Pages.FirstOrDefault(p => p.AllCards.Contains(card));
        if (targetPage == null)
            return;

        vm.SelectedPage = targetPage;
        await Task.Yield();

        var collectionView = await WaitForCardsListAsync(targetPage);
        if (collectionView == null)
            return;

        var targetCard = await EnsureCardVisibleAsync(vm, targetPage, card);
        if (targetCard == null)
            return;

        collectionView.ScrollTo(targetCard, position: ScrollToPosition.Center, animate: false);
    }

    private async Task<CollectionView?> WaitForCardsListAsync(HomePageModel page)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed <= PaneMaterializeTimeout)
        {
            var collectionView = FindCardsList(page);
            if (collectionView != null)
                return collectionView;

            await Task.Delay(PaneMaterializePollDelay);
        }

        return FindCardsList(page);
    }

    private CollectionView? FindCardsList(HomePageModel page)
    {
        if (_cardsListsByPage.TryGetValue(page, out var registered) &&
            IsCardsListForPage(registered, page))
        {
            return registered;
        }

        var visible = MainCarousel.VisibleViews
            .OfType<VisualElement>()
            .Select(v => v.FindByName<CollectionView>("CardsList"))
            .FirstOrDefault(cv => IsCardsListForPage(cv, page));

        if (visible != null)
            _cardsListsByPage[page] = visible;

        return visible;
    }

    private static bool IsCardsListForPage(CollectionView? collectionView, HomePageModel page)
    {
        return collectionView != null && ReferenceEquals(collectionView.BindingContext, page);
    }

    private static async Task<ICardModel?> EnsureCardVisibleAsync(
        HomeViewModel vm,
        HomePageModel page,
        ICardModel card)
    {
        if (page.VisibleCards.Contains(card))
            return card;

        page.ResetVisible();
        await Task.Yield();

        return page.VisibleCards.Contains(card)
            ? card
            : null;
    }

    private void KeepCardsScrollInteractionSuppressed()
    {
        if (BindingContext is not HomeViewModel vm)
            return;

        _cardsScrollSuppressionHandle ??= vm.BeginInteractionSuppression();

        _cardsScrollSettleCts?.Cancel();
        _cardsScrollSettleCts?.Dispose();

        var cts = new CancellationTokenSource();
        _cardsScrollSettleCts = cts;
        _ = ReleaseCardsScrollInteractionAsync(cts);
    }

    private async Task ReleaseCardsScrollInteractionAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(ScrollInteractionSettleDelay, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!ReferenceEquals(_cardsScrollSettleCts, cts))
            {
                cts.Dispose();
                return;
            }

            _cardsScrollSettleCts = null;
            cts.Dispose();
            _cardsScrollSuppressionHandle?.Dispose();
            _cardsScrollSuppressionHandle = null;
        });
    }

    private void ReleaseCardsScrollInteraction()
    {
        _cardsScrollSettleCts?.Cancel();
        _cardsScrollSettleCts?.Dispose();
        _cardsScrollSettleCts = null;

        _cardsScrollSuppressionHandle?.Dispose();
        _cardsScrollSuppressionHandle = null;
    }

    private void KeepCarouselInteractionSuppressed()
    {
        if (BindingContext is not HomeViewModel vm)
            return;

        _carouselScrollSuppressionHandle ??= vm.BeginInteractionSuppression();

        _carouselScrollSettleCts?.Cancel();
        _carouselScrollSettleCts?.Dispose();

        var cts = new CancellationTokenSource();
        _carouselScrollSettleCts = cts;
        _ = ReleaseCarouselInteractionAsync(cts);
    }

    private async Task ReleaseCarouselInteractionAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(ScrollInteractionSettleDelay, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!ReferenceEquals(_carouselScrollSettleCts, cts))
            {
                cts.Dispose();
                return;
            }

            _carouselScrollSettleCts = null;
            cts.Dispose();
            _carouselScrollSuppressionHandle?.Dispose();
            _carouselScrollSuppressionHandle = null;
        });
    }

    private void ReleaseCarouselInteraction()
    {
        _carouselScrollSettleCts?.Cancel();
        _carouselScrollSettleCts?.Dispose();
        _carouselScrollSettleCts = null;

        _carouselScrollSuppressionHandle?.Dispose();
        _carouselScrollSuppressionHandle = null;
    }

    void Carousel_PositionChanged(object? sender, PositionChangedEventArgs e)
    {
        KeepCarouselInteractionSuppressed();

        if (e.CurrentPosition != _lastPos)
        {
            _lastPos = e.CurrentPosition;
            //_audio.Thock();
            //HapticFeedback.Perform(HapticFeedbackType.Click);
        }

    }

    int _lastCenterIndex = -1;
    void Cards_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        KeepCardsScrollInteractionSuppressed();

        if (e.CenterItemIndex != _lastCenterIndex && e.CenterItemIndex >= 0)
        {
            _lastCenterIndex = e.CenterItemIndex;
            //_audio.Tick();
        }
    }

    void Carousel_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        KeepCarouselInteractionSuppressed();
    }

    private bool _hasAppearedOnce = false;
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var isFirstAppearance = !_hasAppearedOnce;
        var homeViewModel = BindingContext as HomeViewModel;

        if (homeViewModel != null)
        {
            if (!_hasAppearedOnce && homeViewModel.Initialization != null)
            {
                try
                {
                    await homeViewModel.Initialization;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Home initialization failed: {ex}");
                }
            }

            homeViewModel.ProcessPendingActiveCardNotificationNavigation();
        }

        StartTicker();
        StartScheduledBackupChecks();
        _hasAppearedOnce = true;

        if (homeViewModel != null)
            await homeViewModel.RefreshMissedNotificationBadgeAsync();

        if (isFirstAppearance && homeViewModel != null)
            await homeViewModel.HandleHomeOpenedForPremiumPromptAsync();
    }

    protected override void OnDisappearing()
    {
        StopTicker();
        StopScheduledBackupChecks();
        ReleaseCardsScrollInteraction();
        ReleaseCarouselInteraction();
        base.OnDisappearing();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent != null)
        {
            _wasParented = true;
            return;
        }

        if (!_wasParented || Interlocked.Exchange(ref _disposeStarted, 1) == 1)
            return;

        StopTicker();
        StopScheduledBackupChecks();
        ReleaseCardsScrollInteraction();
        ReleaseCarouselInteraction();

        if (BindingContext is HomeViewModel vm)
            TaskSupervisor.Forget(vm.DisposeAsync().AsTask(), "Dispose removed Home page");
    }

    private void StartTicker()
    {
        StopTicker();

        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                while (_timer != null && await _timer.WaitForNextTickAsync(_cts.Token))
                {
                    if (BindingContext is HomeViewModel vm)
                        await vm.TickAsync();
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private void StopTicker()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _timer?.Dispose();
        _timer = null;
    }

    private void StartScheduledBackupChecks()
    {
        StopScheduledBackupChecks();

        _scheduledBackupCts = new CancellationTokenSource();
        _scheduledBackupTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        var token = _scheduledBackupCts.Token;
        var timer = _scheduledBackupTimer;

        QueueScheduledBackupCheck();

        _ = Task.Run(async () =>
        {
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    QueueScheduledBackupCheck();
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void QueueScheduledBackupCheck()
    {
        TaskSupervisor.Forget(
            _scheduledBackupRunner.RunDueAsync(),
            "Scheduled automatic export");
    }

    private void StopScheduledBackupChecks()
    {
        _scheduledBackupCts?.Cancel();
        _scheduledBackupCts?.Dispose();
        _scheduledBackupCts = null;

        _scheduledBackupTimer?.Dispose();
        _scheduledBackupTimer = null;
    }

}
