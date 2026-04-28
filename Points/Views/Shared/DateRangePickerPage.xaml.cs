using Points.Global;
using Points.Services.Sqlite.Interfaces;

namespace Points.Views.Shared;

public partial class DateRangePickerPage : ContentPage
{
    private readonly Func<DateTime, DateTime, Task>? _onSaved;

    public DateRangePickerPage(IDbService db, Func<DateTime, DateTime, Task>? onSaved = null)
	{
		InitializeComponent();
        _onSaved = onSaved;

        RangePicker.RangeStart = GlobalVariables.RangeStart;
        RangePicker.RangeEnd = GlobalVariables.RangeEnd;
	}

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var rangeStart = RangePicker.RangeStart;
        var rangeEnd = RangePicker.RangeEnd;

        if (rangeEnd < rangeStart)
        {
            await DisplayAlert("Invalid range", "Range end must be after range start.", "OK");
            return;
        }

        GlobalVariables.RangeStart = rangeStart;
        GlobalVariables.RangeEnd = rangeEnd;

        if (_onSaved != null)
            await _onSaved(GlobalVariables.RangeStart, GlobalVariables.RangeEnd);

        await Shell.Current.Navigation.PopAsync();
    }
}
