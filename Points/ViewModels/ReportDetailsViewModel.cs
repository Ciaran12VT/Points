using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Points.Models;
using Points.Services;
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
        private IDbService _db;

        // For now, “results grid” = list of strings (each string = one row)
        public ObservableCollection<string> Results { get; } = new();

        [ObservableProperty]
        private string resultsMessage = "";

        [ObservableProperty]
        private bool isBusy;

        public ReportDetailsViewModel(ReportModel report, IDbService db)
        {
            Report = report;
            sqlText = report.SQLQuery;
            _db = db;
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

                var results = await _db.ExecuteSelectForReportAsync(sqlText);

                foreach (var result in results)
                {
                    Results.Add(result);   
                }

                UpdateColumns();

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

        public ColumnDefinitionCollection ColumnDefinitions { get; private set; }

        private void UpdateColumns()
        {
            if (Results.Count == 0)
                return;

            var columnCount = Results[0].Split('|').Length;

            ColumnDefinitions = new ColumnDefinitionCollection();
            for (int i = 0; i < columnCount; i++)
                ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            OnPropertyChanged(nameof(ColumnDefinitions));
        }
    }
}
