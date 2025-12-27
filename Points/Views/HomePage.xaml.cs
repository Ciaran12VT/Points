using Points.Helpers;
using Points.Interfaces;
using Points.Models;
using Points.ViewModels;

namespace Points.Views;

public partial class HomePage : ContentPage
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    readonly IAudioFeedback _audio;

    public HomePage(IAudioFeedback audio)
	{
		InitializeComponent();
        _audio = audio;
        if (BindingContext is HomeViewModel vm)
        {
            vm.ScrollToCardRequested = ScrollToCard;
        }
    }

    private void ScrollToCard(ICardModel card)
    {
        // Find the CollectionView in the currently visible page
        var collectionView = this
            .FindDescendants<CollectionView>()
            .FirstOrDefault();

        if (collectionView == null)
            return;

        if (BindingContext is HomeViewModel vm)
        {
            vm.ScrollMainQuestIntoView();

            // If filtered out, clear filters first
            if (!collectionView.ItemsSource.Cast<object>().Contains(card))
            {
                vm.ClearFiltersCommand.Execute(null);
            }
        }

        collectionView.ScrollTo(card, position: ScrollToPosition.Center, animate: true);
    }

    int _lastPos = -1;

    void Carousel_PositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (e.CurrentPosition != _lastPos)
        {
            _lastPos = e.CurrentPosition;
            _audio.Thock();
            HapticFeedback.Perform(HapticFeedbackType.Click);
        }
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
}