using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Reports;

namespace Points.Views.Reports;

public partial class ReportPage : ContentPage
{

	public ReportPage(
        IReportService reports,
        IAppNavigationService navigation,
        IClock clock)
	{
		InitializeComponent();

        BindingContext = new ReportsViewModel(
            reports,
            navigation,
            clock);
    }
}
