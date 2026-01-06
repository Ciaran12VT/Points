using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Points.Models;
using Points.Services;
using System.Collections.ObjectModel;

namespace Points.ViewModels
{
    public sealed partial class ReportsViewModel : Models.ObservableObject
    {
        private IDbService _db;

        public ObservableCollection<ReportsPageModel> Pages { get; } = new();

        public ReportsViewModel(IDbService db)
        {
            _db = db;

            // Mock seed (replace later with SQLite fetch)
            var reports = new ObservableCollection<ReportModel>
            {
                new ReportModel
                {
                    Title = "Dockets - Last 50",
                    SQLQuery = "SELECT * FROM tf_Docket ORDER BY CreatedDate DESC LIMIT 50;"
                },
                new ReportModel
                {
                    Title = "Customers - Search",
                    SQLQuery = "SELECT * FROM mf_Customer WHERE Name LIKE '%hanlon%';"
                }
            };

            Pages.Add(new ReportsPageModel("Reports", reports));
        }

        [RelayCommand]
        private async Task OpenReportAsync(ReportModel? report)
        {
            if (report == null) return;

            // Simple navigation approach: push a page and hand it the model.
            // (No Shell routes required.)
            var page = new Points.Views.Details.ReportDetailsPage
            {
                BindingContext = new ReportDetailsViewModel(report, _db)
            };

            await Application.Current!.MainPage!.Navigation.PushAsync(page);
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
