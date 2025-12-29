using Points.Global;

namespace Points.Views.Shared;

public partial class DateRangePickerPage : ContentPage
{
	public DateRangePickerPage()
	{
		InitializeComponent();
	}

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        GlobalVariables.RangeStart = RangePicker.RangeStart;
        GlobalVariables.RangeEnd = RangePicker.RangeEnd;

        await Shell.Current.Navigation.PopModalAsync();
    }
}