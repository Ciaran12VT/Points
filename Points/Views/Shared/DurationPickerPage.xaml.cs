using System.Globalization;
using Points.Services.Navigation;

namespace Points.Views.Shared;

public partial class DurationPickerPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly TaskCompletionSource<TimeSpan?> _tcs = new();

    public bool WasCancelled { get; private set; }
    public Command CancelCommand { get; }
    public Command ResetCommand { get; }
    public Command OkCommand { get; }

    public DurationPickerPage(TimeSpan? initial, IAppNavigationService navigation)
	{
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        CancelCommand = new Command(async () => await CancelAsync());
        ResetCommand = new Command(async () => await ResetAsync());
        OkCommand = new Command(async () => await ConfirmAsync());

		InitializeComponent();

        HoursEntry = this.FindByName<Entry>("HoursEntry");
        MinutesEntry = this.FindByName<Entry>("MinutesEntry");
        SecondsEntry = this.FindByName<Entry>("SecondsEntry");


        if (initial is not null)
        {
            HoursEntry.Text = ((int)initial.Value.TotalHours).ToString(CultureInfo.InvariantCulture);
            MinutesEntry.Text = initial.Value.Minutes.ToString(CultureInfo.InvariantCulture);
            SecondsEntry.Text = initial.Value.Seconds.ToString(CultureInfo.InvariantCulture);
        }
    }

    public Task<TimeSpan?> Result => _tcs.Task;

    private async Task CancelAsync()
    {
        WasCancelled = true;
        _tcs.TrySetResult(null);
        await _navigation.PopModalAsync();
    }

    private async Task ResetAsync()
    {
        // Reset means: not cancelled, but "no duration"
        WasCancelled = false;
        _tcs.TrySetResult(null);
        await _navigation.PopModalAsync();
    }

    private async Task ConfirmAsync()
    {
        static int ParseInt(string? s)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        var h = Math.Max(0, ParseInt(HoursEntry.Text));
        var m = Math.Max(0, ParseInt(MinutesEntry.Text));
        var s = Math.Max(0, ParseInt(SecondsEntry.Text));

        // Normalize (so 90 seconds becomes 1:30 etc)
        var ts = new TimeSpan(h, 0, 0) + new TimeSpan(0, m, s);

        _tcs.TrySetResult(ts);
        await _navigation.PopModalAsync();
    }
}
