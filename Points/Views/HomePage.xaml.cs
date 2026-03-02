using Points.Helpers;
using Points.Interfaces;
using Points.Models;
using Points.ViewModels;

namespace Points.Views;

public partial class HomePage : ContentPage
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<int>? _posTcs;
    readonly IAudioFeedback _audio;
    int _lastPos = -1;
    private readonly Dictionary<HomePageModel, CollectionView> _cardsListsByPage = new();

    public HomePage(HomeViewModel vm, IAudioFeedback audio)
	{
		InitializeComponent();
        BindingContext = vm;
        _audio = audio;

        vm.ScrollToCardRequested = ScrollToCard;
        vm.ScrollToAnyCardByIdRequested = ScrollToCardById;
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
        var targetPos = vm.GetCardPageIndex(card);
        if (targetPos == -1) return;

        // Step one page at a time
        while (MainCarousel.Position != targetPos)
        {
            MainCarousel.Position += MainCarousel.Position < targetPos ? 1 : -1;
            await Task.Delay(500);
        }

        // Find CardsList in the now-visible pane
        var collectionView = MainCarousel.VisibleViews
            .OfType<VisualElement>()
            .Select(v => v.FindByName<CollectionView>("CardsList"))
            .FirstOrDefault(cv => cv != null);

        if (collectionView == null) return;

        var items = collectionView.ItemsSource?.Cast<object>()?.ToList() ?? new();
        if (!items.Contains(card))
        {
            vm.ClearFiltersCommand.Execute(null);
            await Task.Delay(50);
            items = collectionView.ItemsSource?.Cast<object>()?.ToList() ?? new();
            if (!items.Contains(card)) return;
        }

        collectionView.ScrollTo(card, position: ScrollToPosition.Center, animate: true);
    }

    void Carousel_PositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (e.CurrentPosition != _lastPos)
        {
            _lastPos = e.CurrentPosition;
            _audio.Thock();
            HapticFeedback.Perform(HapticFeedbackType.Click);
        }

        _posTcs?.TrySetResult(e.CurrentPosition);
    }

    int _lastCenterIndex = -1;
    void Cards_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.CenterItemIndex != _lastCenterIndex && e.CenterItemIndex >= 0)
        {
            _lastCenterIndex = e.CenterItemIndex;
            _audio.Tick();
        }
    }

    private async void OnTextSearchClicked(object sender, EventArgs e)
    {
        if (BindingContext is not HomeViewModel vm) return;

        var input = await Shell.Current.DisplayPromptAsync(
            "Search",
            $"Filter Titles and Tags by:",
            accept: "OK",
            cancel: "Cancel",
            placeholder: "e.g. Education",
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(input)) return;

        vm.FilterCardsBySearchTerm(input);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        StartTicker();
    }

    protected override void OnDisappearing()
    {
        StopTicker();
        base.OnDisappearing();
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
                        vm.Tick();
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

    private void OnDebugNotificationClicked(object sender, EventArgs e)
    {
        if(BindingContext is HomeViewModel vm)
        {
            vm.DebugBeep();
        }
    }
}