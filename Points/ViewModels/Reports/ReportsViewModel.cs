using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using System.Collections.ObjectModel;

namespace Points.ViewModels.Reports
{
    public sealed partial class ReportsViewModel : Models.ObservableObject
    {
        private readonly IReportService _reports;
        private readonly IClock _clock;
        private readonly IAppNavigationService _navigation;

        public ObservableCollection<ReportsPageModel> Pages { get; } = new();

        public Command AddReportCommand { get; set; }

        public Task? Initialization { get; private set; }

        public ReportsViewModel(
            IReportService reports,
            IAppNavigationService navigation,
            IClock clock)
        {
            _reports = reports;
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            // Create empty page immediately so UI binds safely
            Pages.Add(new ReportsPageModel("Reports", new ObservableCollection<ReportModel>()));

            AddReportCommand = new Command(async () => await AddReportAsync());

            Initialization = LoadAsync();
        }

        private async Task AddReportAsync()
        {
            await OpenReportAsync(new ReportModel());
        }

        public async Task LoadAsync()
        {
            var page = Pages.First();

            var reports = await _reports.GetReportsAsync();

            page.Cards.Clear();

            foreach (var report in reports)
                page.Cards.Add(report);
        }


        [RelayCommand]
        private async Task OpenReportAsync(ReportModel? report)
        {
            if (report == null) return;

            // Assuming single page for now:
            var cards = Pages[0].Cards;

            var vm = new ReportDetailsViewModel(
                report,
                _reports,
                _clock,
                _navigation,
                onSaved: r =>
                {
                    // If later you allow editing Title and you sort/group, do it here.
                    // For now, the ReportModel reference is shared, so no-op is OK.
                    return Task.CompletedTask;
                },
                onDeleted: r =>
                {
                    // This updates the Reports view immediately
                    if (cards.Contains(r))
                        cards.Remove(r);

                    return Task.CompletedTask;
                });

            var page = new Points.Views.Reports.ReportDetailsPage(vm);
            await _navigation.PushAsync(page);
        }
    }

    public class ReportsPageModel
    {
        public string Name { get; }
        public ObservableCollection<ReportModel> Cards { get; }

        public ReportsPageModel(string name, ObservableCollection<ReportModel> cards)
        {
            Name = name;
            Cards = cards;
        }
    }
}
