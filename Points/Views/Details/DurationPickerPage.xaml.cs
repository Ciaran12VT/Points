using System.Globalization;

namespace Points.Views.Details;

public partial class DurationPickerPage : ContentPage
{
    private readonly TaskCompletionSource<TimeSpan?> _tcs = new();

    public DurationPickerPage(TimeSpan? initial = null)
	{
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

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Shell.Current.Navigation.PopModalAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        static int ParseInt(string? s)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        var h = Math.Max(0, ParseInt(HoursEntry.Text));
        var m = Math.Max(0, ParseInt(MinutesEntry.Text));
        var s = Math.Max(0, ParseInt(SecondsEntry.Text));

        // Normalize (so 90 seconds becomes 1:30 etc)
        var ts = new TimeSpan(h, 0, 0) + new TimeSpan(0, m, s);

        _tcs.TrySetResult(ts);
        await Shell.Current.Navigation.PopModalAsync();
    }

    public static async Task<TimeSpan?> ShowAsync(TimeSpan? initial = null)
    {
        var page = new DurationPickerPage(initial);
        await Shell.Current.Navigation.PushModalAsync(new NavigationPage(page));
        return await page.Result;
    }
}