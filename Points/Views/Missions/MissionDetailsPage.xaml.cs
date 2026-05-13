using Points.Models;
using Points.Services.MissionSharing;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Missions;

namespace Points.Views.Missions;

public partial class MissionDetailsPage : ContentPage
{
    public MissionDetailsPage(
        MissionCardModel model,
        Action<MissionCardModel> onSaved,
        Func<MissionCardModel, Task> onDelete,
        Func<MissionCardModel, Task> onFail,
        Func<MissionCardModel, Task> onRestore,
        List<string> availableTagsList,
        IActivityService activity,
        IUdmdService udmd,
        IMissionShareService missionShares,
        IClock clock,
        ITimeZoneService timeZoneService,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        BindingContext = new MissionDetailsViewModel(
            model,
            onSaved,
            onDelete,
            onFail,
            onRestore,
            availableTagsList,
            activity,
            udmd,
            missionShares,
            clock,
            timeZoneService,
            navigation,
            dialogs);

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await TryFocusTitleIfEmptyAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TryFocusTitleIfEmptyAsync();
    }

    private async Task TryFocusTitleIfEmptyAsync()
    {
        if (TitleEntry == null)
            return;

        if (!string.IsNullOrWhiteSpace(TitleEntry.Text))
            return;

        if (!TitleEntry.IsEnabled || TitleEntry.IsReadOnly || !TitleEntry.IsVisible)
            return;

        await Task.Delay(50);

        for (int i = 0; i < 3; i++)
        {
            MainThread.BeginInvokeOnMainThread(() => TitleEntry.Focus());
            await Task.Delay(50);

            if (TitleEntry.IsFocused)
                return;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is MissionDetailsViewModel vm)
            vm.StopTimer();
    }
}
