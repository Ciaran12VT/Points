using Points.Models;
namespace Points.Services.Sqlite.Interfaces
{
    public interface IReportService
    {
        Task<IReadOnlyList<string>> ExecuteSelectForReportAsync(
            string sql,
            bool includeHeaderRow = true,
            params object?[] args);

        Task UpsertReportAsync(ReportModel report);
        Task DeleteReportAsync(int reportId);
        Task<IReadOnlyList<ReportModel>> GetReportsAsync();
    }



}