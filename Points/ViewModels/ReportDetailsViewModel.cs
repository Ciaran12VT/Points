using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Points.Models;
using Points.Services;
using System.Collections.ObjectModel;

namespace Points.ViewModels
{
    public sealed partial class ReportDetailsViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public ReportModel Report { get; }

        public string Title => Report.Title;

        [ObservableProperty]
        private string sqlText = "";

        private readonly IDbService _db;

        // Each string = "col1|col2|..."
        public ObservableCollection<string> Results { get; } = new();

        [ObservableProperty]
        private string resultsMessage = "";

        [ObservableProperty]
        private bool isBusy;

        // 🔹 Fire this when the results set is ready
        public event Action? ResultsUpdated;

        public ReportDetailsViewModel(ReportModel report, IDbService db)
        {
            Report = report;
            SqlText = report.SQLQuery;
            _db = db;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                Results.Clear();
                ResultsMessage = "Executing...";

                var results = await _db.ExecuteSelectForReportAsync(SqlText);

                foreach (var result in results)
                {
                    Results.Add(result);
                }

                // Persist edited SQL back to model
                Report.SQLQuery = SqlText;

                ResultsMessage = $"{Results.Count} rows returned.";

                // 🔹 Tell the view that the results are ready
                ResultsUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                ResultsMessage = $"ERROR: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
