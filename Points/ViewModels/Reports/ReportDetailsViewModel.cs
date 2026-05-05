using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace Points.ViewModels.Reports
{
    public sealed partial class ReportDetailsViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public IAsyncRelayCommand CopyResultsCommand { get; }


        private readonly Func<ReportModel, Task> _onSaved;
        private readonly Func<ReportModel, Task> _onDeleted;

        public ReportModel Report { get; }

        public string Title => Report.Title;

        private string _titleText = "";
        public string TitleText
        {
            get => _titleText;
            set => SetProperty(ref _titleText, value);
        }


        private string _sqlText = "";
        public string SqlText
        {
            get => _sqlText;
            set => SetProperty(ref _sqlText, value);
        }

        private readonly IReportService _reports;
        private readonly IClock _clock;
        private readonly IAppNavigationService _navigation;

        // Each string = "col1|col2|..."
        public ObservableCollection<string> Results { get; } = new();

        public bool CanCopyResults => !IsBusy && Results.Count > 0;

        private string _resultsMessage = "";
        public string ResultsMessage
        {
            get => _resultsMessage;
            set => SetProperty(ref _resultsMessage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    RefreshCopyAvailability();
            }
        }

        // ?? Fire this when the results set is ready
        public event Action? ResultsUpdated;

        public ReportDetailsViewModel(
            ReportModel report,
            IReportService reports,
            IClock clock,
            IAppNavigationService navigation,
            Func<ReportModel, Task>? onSaved = null,
            Func<ReportModel, Task>? onDeleted = null)
        {
            Report = report;
            TitleText = report.Title;
            SqlText = report.SQLQuery;
            _reports = reports;
            _clock = clock;
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));


            _onSaved = onSaved ?? (_ => Task.CompletedTask);
            _onDeleted = onDeleted ?? (_ => Task.CompletedTask);

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy);
            CopyResultsCommand = new AsyncRelayCommand(CopyResultsAsync, () => CanCopyResults);
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
                await _navigation.PopAsync();
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
                RefreshCopyAvailability();
                ResultsMessage = "Executing...";

                var results = await _reports.ExecuteSelectForReportAsync(SqlText);

                foreach (var result in results)
                {
                    Results.Add(result);
                }

                // Persist edited SQL back to model
                Report.SQLQuery = SqlText;

                ResultsMessage = $"{Results.Count} rows returned.";
                RefreshCopyAvailability();

                // ?? Tell the view that the results are ready
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

        private async Task CopyResultsAsync()
        {
            if (!CanCopyResults) return;

            try
            {
                await Clipboard.Default.SetTextAsync(ConvertResultsToCsv());
                ResultsMessage = $"{Results.Count} rows copied to clipboard.";
            }
            catch (Exception ex)
            {
                ResultsMessage = $"ERROR (Copy): {ex.Message}";
            }
        }

        private string ConvertResultsToCsv()
        {
            var builder = new StringBuilder();

            for (var i = 0; i < Results.Count; i++)
            {
                if (i > 0)
                    builder.AppendLine();

                builder.Append(ConvertResultRowToCsv(Results[i]));
            }

            return builder.ToString();
        }

        private static string ConvertResultRowToCsv(string? row)
        {
            var cells = (row ?? string.Empty)
                .Split('|')
                .Select(cell => EscapeCsvCell(cell.Trim()));

            return string.Join(",", cells);
        }

        private static string EscapeCsvCell(string cell)
        {
            if (cell.Contains('"'))
                cell = cell.Replace("\"", "\"\"");

            return cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                ? $"\"{cell}\""
                : cell;
        }

        private void RefreshCopyAvailability()
        {
            OnPropertyChanged(nameof(CanCopyResults));
            CopyResultsCommand?.NotifyCanExecuteChanged();
        }
    }
}
