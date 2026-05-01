using Points.Global;
using Points.Services.Navigation;
using Points.Services.Time;

namespace Points.Views.Shared;

public partial class DateRangePickerPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly IClock _clock;
    private readonly Func<DateTime, DateTime, Task>? _onSaved;

    public Command SaveCommand { get; }

    public DateRangePickerPage(
        Func<DateTime, DateTime, Task>? onSaved,
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

        RangePicker.CurrentLocalNow = _clock.LocalNow;
        RangePicker.RangeStart = GlobalVariables.RangeStart;
        RangePicker.RangeEnd = GlobalVariables.RangeEnd;
	}

    private async Task SaveAsync()
    {
        var rangeStart = RangePicker.RangeStart;
        var rangeEnd = RangePicker.RangeEnd;

        if (rangeEnd < rangeStart)
        {
            await _dialogs.DisplayAlertAsync("Invalid range", "Range end must be after range start.", "OK");
            return;
        }

        GlobalVariables.RangeStart = rangeStart;
        GlobalVariables.RangeEnd = rangeEnd;

        if (_onSaved != null)
            await _onSaved(GlobalVariables.RangeStart, GlobalVariables.RangeEnd);

        await _navigation.PopAsync();
    }
}
