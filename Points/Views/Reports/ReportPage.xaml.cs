using Points.ViewModels;

namespace Points.Views.Reports;

public partial class ReportPage : ContentPage
{
	public ReportPage(Services.IDbService _db)
	{
		InitializeComponent();

        BindingContext = new ReportsViewModel();
    }
}