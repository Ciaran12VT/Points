using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Points.Models;
using System.Collections.ObjectModel;
using System.Xml;

namespace Points.ViewModels
{
    public sealed partial class ReportDetailsViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public ReportModel Report { get; }

        public string Title => Report.Title;

        [ObservableProperty]
        private string sqlText = "";

        // For now, “results grid” = list of strings (each string = one row)
        public ObservableCollection<string> Results { get; } = new();

        [ObservableProperty]
        private string resultsMessage = "";

        [ObservableProperty]
        private bool isBusy;

        public ReportDetailsViewModel(ReportModel report)
        {
            Report = report;
            sqlText = report.SQLQuery;
        }

        [RelayCommand]
        private async Task ExecuteAsync()
        {
            if (isBusy) return;
            isBusy = true;

            try
            {
                Results.Clear();
                resultsMessage = "Executing...";

                // TODO: Replace with real execution service (SQLite / SQL Server / API)
                await Task.Delay(250);

                // Fake output for now
                Results.Add("Row 1: { Id: 1, Name: \"Example\" }");
                Results.Add("Row 2: { Id: 2, Name: \"Example 2\" }");
                resultsMessage = $"OK ({Results.Count} rows)";

                // Persist edited SQL back to model (optional but usually handy)
                Report.SQLQuery = sqlText;
            }
            catch (Exception ex)
            {
                resultsMessage = $"ERROR: {ex.Message}";
            }
            finally
            {
                isBusy = false;
            }
        }
    }
}
