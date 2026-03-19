using Points.Global;

namespace Points.Views.Shared;

public partial class DateRangePickerPage : ContentPage
{

/* Unmerged change from project 'Points (net8.0-android)'
Before:
	public DateRangePickerPage(Services.IDbService _db)
	{
After:
	public DateRangePickerPage(IDbService _db)
	{
*/

/* Unmerged change from project 'Points (net8.0-android)'
Before:
	public DateRangePickerPage(Services.Sqlite.Interfaces.IDbService _db)
	{
After:
	public DateRangePickerPage(IDbService _db)
	{
*/
	public DateRangePickerPage(Services.Sqlite.Services.Interfaces.IDbService _db)
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