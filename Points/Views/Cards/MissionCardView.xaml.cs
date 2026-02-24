using CommunityToolkit.Maui.Behaviors;
using Points.Helpers;
using Points.Models;
using Points.Services.Locks;
using Points.ViewModels;
using Points.Views.Details;
using System.ComponentModel;

namespace Points.Views.Cards;

public partial class MissionCardView : ContentView
{
    private TouchBehavior? _touch;

    public MissionCardView()
	{
		InitializeComponent();
	}

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not MissionCardModel model)
            return;

        //Prompt the user to confirm if the want to mark this mission as complete

        // For existing cards, Save should NOT add a new card.
        // We'll use the callback to request a refresh/sort if desired.
        Action<MissionCardModel> onSaved = _ => { };
        Action<MissionCardModel> onDelete = _ => { };
        Action<MissionCardModel> onFail = _ => { };

        // If you want to re-sort missions after editing (recommended):
        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            await vm.OpenExistingCardAsync((IActiveCardModel)BindingContext);
        }
    }

    private async void OnCompleteClicked(object sender, EventArgs e)
    {
        if (BindingContext is not MissionCardModel model)
            return;

        var page = this.FindParentOfType<ContentPage>();
        if (page?.BindingContext is HomeViewModel vm)
        {
            var now = (Shell.Current?.CurrentPage?.BindingContext as HomeViewModel)?.Now ?? DateTime.Now;

            if (LockEvaluator.IsLockedNow(model, now, vm.GetActiveCardModels(), out var availableAt))
            {
                var rem = LockEvaluator.FormatRemaining(now, availableAt);
                await Shell.Current.DisplayAlert("Locked", $"This mission is locked. Available in {rem}.", "OK");
                return;
            }

            if (!model.IsComplete)
            {
                bool confirm = await page.DisplayAlert(
                "Complete mission?",
                    $"Mark as complete?",
                    "Complete",
                    "Cancel");

                if (confirm)
                {
                    // Option A: if the model exposes a CompleteCommand (like your XAML implies)
                    if (model.CompleteCommand?.CanExecute(null) == true)
                        model.CompleteCommand.Execute(null);

                    await Task.Yield();
                    await vm.SaveMission(model);
                    
                }
            }
        }
    }

    #region Button Color Toggle Logic

    private MissionCardModel? _model;
    private HomeViewModel? _homeVm;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // BindingContext may change due to CollectionView recycling
        DetachAll();

        _model = BindingContext as MissionCardModel;
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
        if (e.PropertyName == nameof(MissionCardModel.IsActive))
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