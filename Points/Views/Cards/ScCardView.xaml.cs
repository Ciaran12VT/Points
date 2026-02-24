using Points.Helpers;
using Points.Models;
using Points.Services.Locks;
using Points.ViewModels;
using Points.Views.Details;
using System.ComponentModel;

namespace Points.Views.Cards;

public partial class ScCardView : ContentView
{
	public ScCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not ScCardModel model)
            return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }
    }


    private async void OnAddRepClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ScCardModel model) return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is not HomeViewModel vm) return;

        var now = (Shell.Current?.CurrentPage?.BindingContext as dynamic)?.Now as DateTime? ?? DateTime.Now;

        if (LockEvaluator.IsLockedNow(model, now, vm.GetActiveCardModels(), out var availableAt))
        {
            var rem = LockEvaluator.FormatRemaining(now, availableAt);
            await Shell.Current.DisplayAlert("Locked", $"This card is locked. Available in {rem}.", "OK");
            return;
        }

        if (model.Steps[0].IncrementCommand.CanExecute(null))
        {
            model.Steps[0].IncrementCommand.Execute(null);
            await Task.Yield();
            await vm.IncrementFirstStep(model);
        }
    }


    #region Button Color Toggle Logic

    private ScCardModel? _model;
    private HomeViewModel? _homeVm;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // BindingContext may change due to CollectionView recycling
        DetachAll();

        _model = BindingContext as ScCardModel;
        if (_model == null)
            return;

        // 1) Listen to model IsActive changes (instant updates)
        _model.PropertyChanged += OnModelPropertyChanged;

        // 2) Try to hook into HomeViewModel Tick (fallback healing)
        TryAttachToHomeVm();

        // Ensure initial correct colour
        UpdateToggleColor();
    }

    private void TryAttachToHomeVm()
    {
        // Find the page each time; don’t assume parent is ready in ctor
        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is not HomeViewModel vm)
            return;

        _homeVm = vm;
        _homeVm.TickHappened += OnTickHappened;
    }

    private void OnTickHappened()
    {
        // Tick could be fired from non-UI thread depending on your timer
        MainThread.BeginInvokeOnMainThread(UpdateToggleColor);
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScCardModel.IsActive))
            MainThread.BeginInvokeOnMainThread(UpdateToggleColor);
    }

    private void UpdateToggleColor()
    {
        if (_model == null)
            return;

        // Match your intended semantics:
        // Active => Green, Inactive => Gray
        ActivityToggleButton.BackgroundColor = _model.IsActive ? Colors.Green : Colors.Gray;
    }

    private void DetachAll()
    {
        if (_model != null)
            _model.PropertyChanged -= OnModelPropertyChanged;

        if (_homeVm != null)
            _homeVm.TickHappened -= OnTickHappened;

        _model = null;
        _homeVm = null;
    }

    // Your existing handler can now just call UpdateToggleColor()
    private void OnActivityToggleButtonClicked(object sender, EventArgs e)
    {
        UpdateToggleColor();
    }

    #endregion


}