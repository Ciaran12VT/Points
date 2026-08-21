using Points.Global;
using Points.Services.Navigation;
using Points.Services.Time;

namespace Points.Views.Shared;

public partial class DateRangePickerPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly IClock _clock;
    private readonly Func<DateTime, DateTime, bool, Task>? _onSaved;

    public Command SaveCommand { get; }

    public DateRangePickerPage(
        Func<DateTime, DateTime, bool, Task>? onSaved,
        IClock clock,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
	{
        SaveCommand = new Command(async () => await SaveAsync());

        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _onSaved = onSaved;

        var now = _clock.LocalNow;
        var currentRange = GlobalVariables.GetCurrentRange(now);
        RangePicker.CurrentLocalNow = now;
        RangePicker.InitializeRange(
            currentRange.Start,
            currentRange.End,
            currentRange.FollowsCurrentDay);
	}

    private async Task SaveAsync()
    {
        var localNow = TimeDisplayFormatter.ToLocalInstant(_clock.LocalNow);
        RangePicker.CurrentLocalNow = localNow;

        if (RangePicker.FollowsCurrentDay)
        {
            RangePicker.InitializeRange(
                localNow.Date,
                localNow.Date.AddDays(1).AddTicks(-1),
                followsCurrentDay: true);
        }

        var rangeStart = RangePicker.RangeStart;
        var rangeEnd = RangePicker.RangeEnd;

        if (rangeEnd < rangeStart)
        {
            await _dialogs.DisplayAlertAsync("Invalid range", "Range end must be after range start.", "OK");
            return;
        }

        if (_onSaved != null)
        {
            await _onSaved(rangeStart, rangeEnd, RangePicker.FollowsCurrentDay);
        }
        else
        {
            GlobalVariables.SetRange(
                rangeStart,
                rangeEnd,
                localNow,
                RangePicker.FollowsCurrentDay);
        }

        await _navigation.PopAsync();
    }
}
