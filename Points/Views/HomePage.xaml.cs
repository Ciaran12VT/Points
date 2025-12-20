using Points.ViewModels;

namespace Points.Views;

public partial class HomePage : ContentPage
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    public HomePage()
	{
		InitializeComponent();
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

    private async void OnAddClicked(object sender, EventArgs e)
    {
        if (BindingContext is HomeViewModel vm)
            vm.AddCardToCurrentPage();

        await Task.CompletedTask;
    }

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Tapped", "Card tapped!", "OK");
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