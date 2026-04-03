using CommunityToolkit.Maui.Views;
using Points.Global;
using Points.Helpers;
using Points.Models;
using Points.ViewModels;
using Points.Views.Details;
using System.ComponentModel;

namespace Points.Views.Cards;

public partial class TatCardView : ContentView
{
    public TatCardView()
	{
		InitializeComponent();

       // Unloaded += (_, __) => DetachAll();
    }

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not TatCardModel model) return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }

    }

    //#region Button Color Toggle Logic

    //private TatCardModel? _model;
    //private HomeViewModel? _homeVm;

    //protected override void OnBindingContextChanged()
    //{
    //    base.OnBindingContextChanged();

    //    // BindingContext may change due to CollectionView recycling
    //    DetachAll();

    //    _model = BindingContext as TatCardModel;
    //    if (_model == null)
    //        return;

    //    // 1) Listen to model IsActive changes (instant updates)
    //    _model.PropertyChanged += OnModelPropertyChanged;

    //    // 2) Try to hook into HomeViewModel Tick (fallback healing)
    //    TryAttachToHomeVm();

    //    // Ensure initial correct colour
    //    UpdateToggleColor();
    //}

    //private void TryAttachToHomeVm()
    //{
    //    // Find the page each time; don’t assume parent is ready in ctor
    //    var page = this.FindParentOfType<ContentPage>();
    //    if (page?.BindingContext is not HomeViewModel vm)
    //        return;

    //    _homeVm = vm;
    //    _homeVm.TickHappened += OnTickHappened;
    //}

    //private void OnTickHappened()
    //{
    //    // Tick could be fired from non-UI thread depending on your timer
    //    MainThread.BeginInvokeOnMainThread(UpdateToggleColor);
    //}

    //private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(TatCardModel.IsActive))
    //        MainThread.BeginInvokeOnMainThread(UpdateToggleColor);
    //}

    //private void UpdateToggleColor()
    //{
    //    if (_model == null)
    //        return;

    //    // Match your intended semantics:
    //    // Active => Green, Inactive => Gray
    //    ActivityToggleButton.BackgroundColor = _model.IsActive ? Colors.Green : Colors.Gray;
    //}

    //private void DetachAll()
    //{
    //    if (_model != null)
    //        _model.PropertyChanged -= OnModelPropertyChanged;

    //    if (_homeVm != null)
    //        _homeVm.TickHappened -= OnTickHappened;

    //    _model = null;
    //    _homeVm = null;
    //}

    //// Your existing handler can now just call UpdateToggleColor()
    //private void OnActivityToggleButtonClicked(object sender, EventArgs e)
    //{
    //    if (_longPressFired)
    //        return;

    //    UpdateToggleColor();
    //}

    //#endregion


    private CancellationTokenSource? _longPressCts;
    private bool _longPressFired;
    private const int LongPressMs = 600;

    private void ActivityToggleButton_Pressed(object sender, EventArgs e)
    {
        // New press => cancel any previous pending
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();

        _longPressCts = new CancellationTokenSource();
        _longPressFired = false;

        var token = _longPressCts.Token;

        // IMPORTANT: capture the sender, not the view's BindingContext.
        // In recycled cells, sender.BindingContext is the truth at press-time.
        if (sender is not Button btn) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(LongPressMs, token);
                if (token.IsCancellationRequested) return;

                _longPressFired = true;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (btn.BindingContext is IActiveCardModel card)
                    {
                        card.FireLongPressRequested(card);
                    }
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void ActivityToggleButton_Released(object sender, EventArgs e)
    {
        // Release cancels pending long-press (if not fired)
        _longPressCts?.Cancel();
    }


}