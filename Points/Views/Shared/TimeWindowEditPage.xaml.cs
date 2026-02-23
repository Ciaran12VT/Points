namespace Points.Views.Shared;

public partial class TimeWindowEditPage : ContentPage
{
    private readonly TaskCompletionSource<(TimeOnly Start, TimeOnly End)> _tcs;

    public TimeWindowEditPage( TimeOnly? initialStart, TimeOnly? initialEnd, TaskCompletionSource<(TimeOnly Start, TimeOnly End)> tcs)
    {
        InitializeComponent();

        _tcs = tcs;

        // Defaults per spec: 00:00:00 -> 23:59:59
        var start = initialStart ?? new TimeOnly(0, 0, 0);
        var end = initialEnd ?? new TimeOnly(23, 59, 59);

        StartPicker.Time = start.ToTimeSpan();
        EndPicker.Time = end.ToTimeSpan();
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        var start = TimeOnly.FromTimeSpan(StartPicker.Time);
        var end = TimeOnly.FromTimeSpan(EndPicker.Time);

        if (start > end)
        {
            await DisplayAlert("Invalid time window", "Start cannot be later than End.", "OK");
            return;
        }

        _tcs.TrySetResult((start, end));
        await Navigation.PopAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetCanceled();
        return base.OnBackButtonPressed();
    }
}