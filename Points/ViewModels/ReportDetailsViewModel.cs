using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Points.ViewModels
{
    public sealed partial class ReportDetailsViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }


        private readonly Func<ReportModel, Task> _onSaved;
        private readonly Func<ReportModel, Task> _onDeleted;

        public ReportModel Report { get; }

        public string Title => Report.Title;

        [ObservableProperty]
        private string titleText = "";


        [ObservableProperty]
        private string sqlText = "";

        private readonly IReportService _reports;
        private readonly IClock _clock;

        // Each string = "col1|col2|..."
        public ObservableCollection<string> Results { get; } = new();

        [ObservableProperty]
        private string resultsMessage = "";

        [ObservableProperty]
        private bool isBusy;

        // 🔹 Fire this when the results set is ready
        public event Action? ResultsUpdated;

        public ReportDetailsViewModel(ReportModel report, IReportService reports, IClock clock, Func<ReportModel, Task>? onSaved = null, Func<ReportModel, Task>? onDeleted = null)
        {
            Report = report;
            TitleText = report.Title;
            SqlText = report.SQLQuery;
            _reports = reports;
            _clock = clock;


            _onSaved = onSaved ?? (_ => Task.CompletedTask);
            _onDeleted = onDeleted ?? (_ => Task.CompletedTask);

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy);
        }

        private async Task SaveAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // Persist edits back into the shared model
                Report.Title = TitleText.Trim();
                Report.SQLQuery = SqlText;
                Report.LastRunOn = _clock.UtcNow;

                await _reports.UpsertReportAsync(Report);

                // Notify parent list (for resorting/requery/etc.)
                await _onSaved(Report);

                ResultsMessage = "Saved.";
            }
            catch (Exception ex)
            {
                ResultsMessage = $"ERROR (Save): {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                await _reports.DeleteReportAsync(Report.Id);

                // Update parent list
                await _onDeleted(Report);

                // Close details page
                await Application.Current!.MainPage!.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                ResultsMessage = $"ERROR (Delete): {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
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

                var results = await _reports.ExecuteSelectForReportAsync(SqlText);

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
