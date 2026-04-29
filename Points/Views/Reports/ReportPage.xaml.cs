using Points.Helpers;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Reports;

public partial class ReportPage : ContentPage
{

	public ReportPage(IReportService reports)
	{
		InitializeComponent();

        BindingContext = new ReportsViewModel(reports, ServiceHelper.GetService<IClock>());
    }
}
