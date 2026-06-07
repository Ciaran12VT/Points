using Points.Services.Navigation;

namespace Points.Views.Shared;

public partial class TimeWindowEditPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly TaskCompletionSource<(TimeOnly Start, TimeOnly End)> _tcs;

    public Command DoneCommand { get; }

    public TimeWindowEditPage(
        TimeOnly? initialStart,
        TimeOnly? initialEnd,
        TaskCompletionSource<(TimeOnly Start, TimeOnly End)> tcs,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        DoneCommand = new Command(async () => await DoneAsync());

        InitializeComponent();

        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _tcs = tcs;

        // Defaults per spec: 00:00:00 -> 23:59:59
        var start = initialStart ?? new TimeOnly(0, 0, 0);
        var end = initialEnd ?? new TimeOnly(23, 59, 59);

        StartPicker.Time = start.ToTimeSpan();
        EndPicker.Time = end.ToTimeSpan();
    }

    private async Task DoneAsync()
    {
        var start = TimeOnly.FromTimeSpan(StartPicker.Time);
        var end = TimeOnly.FromTimeSpan(EndPicker.Time);

        if (start > end)
        {
            await _dialogs.DisplayAlertAsync("Invalid time window", "Start cannot be later than End.", "OK");
            return;
        }

        _tcs.TrySetResult((start, end));
        await _navigation.PopAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetCanceled();
        return base.OnBackButtonPressed();
    }
}
