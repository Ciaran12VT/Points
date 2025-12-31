using Points.ViewModels;

namespace Points.Views.Reports;

public partial class ReportPage : ContentPage
{
	public ReportPage()
	{
		InitializeComponent();

        BindingContext = new ReportsViewModel();
    }
}