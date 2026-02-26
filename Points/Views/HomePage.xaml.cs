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

    public HomePage(HomeViewModel vm, IAudioFeedback audio)
	{
		InitializeComponent();
        BindingContext = vm;
        _audio = audio;

        vm.ScrollToCardRequested = ScrollToCard;
        vm.ScrollToAnyCardByIdRequested = ScrollToCardById;
    }

    //private async void ScrollToCard(IActiveCardModel card)
    //{
    //    if (BindingContext is not HomeViewModel vm) return;

    //    // This should set vm.Position to the correct page index (via your VM logic)
    //    vm.ScrollCardPageIntoView(card);

    //    // Wait until the Carousel has actually switched to that position
    //    var targetPos = vm.Position;

    //    if (MainCarousel.Position != targetPos)
    //    {
    //        _posTcs = new TaskCompletionSource<int>();

    //        // If Position is bound TwoWay, setting it here is fine too (optional safety):
    //        MainCarousel.Position = targetPos;

    //        while (true)
    //        {
    //            var pos = await _posTcs.Task;       // completed by Carousel_PositionChanged
    //            if (pos == targetPos) break;
    //            _posTcs = new TaskCompletionSource<int>();
    //        }
    //    }
    //    else
    //    {
    //        // Even when already at the right position, yield a frame so the template is ready.
    //        await Task.Yield();
    //    }

    //    // Now get the CollectionView for the CURRENT visible carousel page (not the first one in the tree)
    //    var currentPageVm = vm.Pages[targetPos];

    //    var currentPageView =
    //        MainCarousel.FindDescendants<VisualElement>()
    //                    .FirstOrDefault(v => v.BindingContext == currentPageVm);

    //    var collectionView = currentPageView?.FindByName<CollectionView>("CardsList");
    //    if (collectionView == null) return;

    //    if (!collectionView.ItemsSource.Cast<object>().Contains(card))
    //        vm.ClearFiltersCommand.Execute(null);

    //    collectionView.ScrollTo(card, position: ScrollToPosition.Center, animate: true);
    //}

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
        // 1) Switch carousel page (VM sets Position)
        vm.ScrollCardPageIntoView(card);

        var targetPos = vm.Position;

        // 2) Wait for carousel to actually reach that position
        if (MainCarousel.Position != targetPos)
        {
            _posTcs = new TaskCompletionSource<int>();

            // optional safety (since Position is TwoWay)
            MainCarousel.Position = targetPos;

            while (true)
            {
                var pos = await _posTcs.Task; // completed in Carousel_PositionChanged
                if (pos == targetPos) break;

                _posTcs = new TaskCompletionSource<int>();
            }
        }
        else
        {
            await Task.Yield();
        }

        // 3) Find the CURRENT visible pane view for vm.Pages[targetPos]
        var currentPageVm = vm.Pages[targetPos];

        var currentPageView =
            MainCarousel.FindDescendants<VisualElement>()
                        .FirstOrDefault(v => v.BindingContext == currentPageVm);

        // IMPORTANT: Dashboard pane won't have "CardsList".
        // Shortcuts target *other* panes, so this should normally exist.
        var collectionView = currentPageView?.FindByName<CollectionView>("CardsList");
        if (collectionView == null)
            return;

        // 4) Ensure item is visible (filters may hide it)
        var items = collectionView.ItemsSource?.Cast<object>()?.ToList() ?? new List<object>();

        if (!items.Contains(card))
        {
            vm.ClearFiltersCommand.Execute(null);

            // Re-evaluate items after filters cleared
            items = collectionView.ItemsSource?.Cast<object>()?.ToList() ?? new List<object>();
            if (!items.Contains(card))
                return; // still not present (wrong page / not loaded / etc.)
        }

        // 5) Scroll
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