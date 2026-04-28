using Points.Helpers;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Reports;

public partial class ReportPage : ContentPage
{

/* Unmerged change from project 'Points (net8.0-android)'
Before:
	public ReportPage(Services.IDbService _db)
	{
After:
	public ReportPage(IDbService _db)
	{
*/
	public ReportPage(Services.Sqlite.Interfaces.IDbService _db)
	{
		InitializeComponent();

        BindingContext = new ReportsViewModel(_db, ServiceHelper.GetService<IClock>());
    }
}
