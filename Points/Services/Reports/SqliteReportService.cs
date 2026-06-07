using Points.Global;
using Points.Services.Sqlite;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;
using SQLitePCL;
using System.Diagnostics;
using System.Globalization;

namespace Points.Services.Reports
{
    public sealed class SqliteReportService : IReportService
    {
        private readonly ISqliteConnectionContext _context;
        private readonly ITimeZoneService? _timeZoneService;
        private readonly ISettingsService? _settings;

        public SqliteReportService(
            ISqliteConnectionContext context,
            ITimeZoneService? timeZoneService = null,
            ISettingsService? settings = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _timeZoneService = timeZoneService;
            _settings = settings;
        }

        /// <summary>
        /// Executes a guarded SELECT or WITH query and returns display lines for the reports UI.
        /// </summary>
        public async Task<IReadOnlyList<string>> ExecuteSelectForReportAsync(
            string sql,
            bool includeHeaderRow = true,
            params object?[] args)
        {
            await _context.InitializeAsync();

            if (string.IsNullOrWhiteSpace(sql))
                return Array.Empty<string>();

            var guardedSql = ReportSqlGuard.ValidateSelectStatement(sql);
            var timeout = await GetReportQueryTimeoutAsync();

            return await Task.Run(() =>
            {
                sqlite3? db = null;
                sqlite3_stmt? stmt = null;
                var stopwatch = Stopwatch.StartNew();
                delegate_progress? progressHandler = null;

                try
                {
                    var rc = raw.sqlite3_open_v2(
                        _context.DatabasePath,
                        out db,
                        raw.SQLITE_OPEN_READONLY,
                        null);

                    if (rc != raw.SQLITE_OK || db == null)
                        throw new InvalidOperationException($"Failed to open SQLite database. rc={rc}");

                    raw.sqlite3_busy_timeout(db, ToMilliseconds(timeout));

                    progressHandler = _ => stopwatch.Elapsed > timeout ? 1 : 0;
                    raw.sqlite3_progress_handler(db, 1000, progressHandler, null);

                    rc = raw.sqlite3_prepare_v2(db, guardedSql, out stmt);
                    if (rc != raw.SQLITE_OK || stmt == null)
                        throw new InvalidOperationException($"sqlite3_prepare_v2 failed. rc={rc}. {raw.sqlite3_errmsg(db).utf8_to_string()}");

                    if (raw.sqlite3_stmt_readonly(stmt) == 0)
                        throw new InvalidOperationException("Only read-only report statements are allowed.");

                    if (args is { Length: > 0 })
                    {
                        for (var i = 0; i < args.Length; i++)
                            BindParameter(stmt, i + 1, args[i]);
                    }

                    var results = new List<string>();
                    var colCount = raw.sqlite3_column_count(stmt);

                    if (includeHeaderRow && colCount > 0)
                    {
                        var headers = new string[colCount];
                        for (var c = 0; c < colCount; c++)
                        {
                            var name = raw.sqlite3_column_name(stmt, c).utf8_to_string();
                            headers[c] = string.IsNullOrEmpty(name) ? $"Col{c + 1}" : name;
                        }

                        results.Add(string.Join(" | ", headers));
                    }

                    while (true)
                    {
                        ThrowIfTimedOut(stopwatch, timeout);

                        rc = raw.sqlite3_step(stmt);

                        if (rc == raw.SQLITE_ROW)
                        {
                            if (results.Count - (includeHeaderRow ? 1 : 0) >= ReportSqlGuard.DefaultMaxRows)
                            {
                                results.Add($"(results truncated at {ReportSqlGuard.DefaultMaxRows} rows)");
                                break;
                            }

                            var row = new string[colCount];
                            for (var c = 0; c < colCount; c++)
                                row[c] = ReadColumnAsText(stmt, c);

                            results.Add(string.Join(" | ", row));
                            continue;
                        }

                        if (rc == raw.SQLITE_DONE)
                            break;

                        if (rc == raw.SQLITE_INTERRUPT && stopwatch.Elapsed > timeout)
                            throw CreateTimeoutException(timeout);

                        throw new InvalidOperationException($"sqlite3_step failed. rc={rc}. {raw.sqlite3_errmsg(db).utf8_to_string()}");
                    }

                    if (results.Count == 0)
                        results.Add("(no rows)");

                    return (IReadOnlyList<string>)results;
                }
                finally
                {
                    if (stmt != null) raw.sqlite3_finalize(stmt);
                    if (db != null) raw.sqlite3_progress_handler(db, 0, null, null);
                    if (db != null) raw.sqlite3_close(db);
                }
            });
        }

        public async Task UpsertReportAsync(ReportModel report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            if (string.IsNullOrWhiteSpace(report.Title))
                throw new ArgumentException("Report.Title is required.", nameof(report));

            await _context.RunInTransactionAsync(conn =>
            {
                var lastRunOn = ToDbDateTime(report.LastRunOn);
                var eligible = report.EligibleForAchievment ? 1 : 0;
                var sql = report.SQLQuery ?? string.Empty;

                if (report.Id > 0)
                {
                    const string updateSql = @"
                    UPDATE Report
                    SET Title = ?,
                        SQLQuery = ?,
                        LastRunOn = ?,
                        EligibleForAchievment = ?
                    WHERE Id = ?;";

                    conn.Execute(updateSql, report.Title, sql, lastRunOn, eligible, report.Id);
                    return;
                }

                const string upsertByTitleSql = @"
                    INSERT INTO Report (Title, SQLQuery, LastRunOn, EligibleForAchievment)
                    VALUES (?, ?, ?, ?)
                    ON CONFLICT(Title) DO UPDATE SET
                        SQLQuery = excluded.SQLQuery,
                        LastRunOn = excluded.LastRunOn,
                        EligibleForAchievment = excluded.EligibleForAchievment;";

                conn.Execute(upsertByTitleSql, report.Title, sql, lastRunOn, eligible);

                var idRow = conn.Query<IdRow>(
                    "SELECT Id FROM Report WHERE Title = ? LIMIT 1;",
                    report.Title).FirstOrDefault();

                if (idRow != null)
                    report.Id = idRow.Id;
            });
        }

        public Task DeleteReportAsync(int reportId)
        {
            return _context.RunInTransactionAsync(conn =>
            {
                conn.Execute("DELETE FROM Report WHERE Id = ?;", reportId);
            });
        }

        public async Task<IReadOnlyList<ReportModel>> GetReportsAsync()
        {
            await _context.InitializeAsync();

            const string sql = @"
                SELECT
                    r.Id                    AS Id,
                    r.Title                 AS Title,
                    r.SQLQuery              AS SQLQuery,
                    r.LastRunOn             AS LastRunOn,
                    r.EligibleForAchievment AS EligibleForAchievment
                FROM Report r
                ORDER BY r.Title;";

            var rows = await _context.Db.QueryAsync<ReportRow>(sql);
            return rows.Select(MapReportRow).ToList();
        }

        private string? ToDbDateTime(DateTime? value)
        {
            if (!value.HasValue)
                return null;

            if (value.Value.Kind == DateTimeKind.Utc)
                return StrictTimeSerializer.SerializeUtcInstant(value.Value);

            return _timeZoneService == null
                ? StrictTimeSerializer.SerializeLocalDateTime(value.Value)
                : StrictTimeSerializer.SerializeUtcInstantFromLocal(value.Value, _timeZoneService);
        }

        private DateTime? FromDbDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (StrictTimeSerializer.TryParseUtcInstant(value, out var utcInstant))
                return utcInstant;

            if (_timeZoneService != null &&
                LegacyTimeReader.TryReadInstantUtc(value, _timeZoneService, out var legacyInstant) &&
                legacyInstant != null)
            {
                return legacyInstant.UtcInstant;
            }

            return LegacyTimeReader.TryReadLocalDateTime(value, out var localDateTime) && localDateTime != null
                ? localDateTime.LocalDateTime
                : throw new FormatException($"Could not parse report LastRunOn value '{value}'.");
        }

        private void BindParameter(sqlite3_stmt stmt, int index, object? value)
        {
            if (value == null)
            {
                raw.sqlite3_bind_null(stmt, index);
                return;
            }

            switch (value)
            {
                case string s:
                    raw.sqlite3_bind_text(stmt, index, s);
                    return;

                case bool b:
                    raw.sqlite3_bind_int(stmt, index, b ? 1 : 0);
                    return;

                case byte or short or int or long:
                    raw.sqlite3_bind_int64(stmt, index, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    return;

                case float or double or decimal:
                    raw.sqlite3_bind_double(stmt, index, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    return;

                case DateTime dt:
                    raw.sqlite3_bind_text(stmt, index, ToDbDateTime(dt) ?? "");
                    return;

                default:
                    raw.sqlite3_bind_text(stmt, index, value.ToString() ?? "");
                    return;
            }
        }

        private static string ReadColumnAsText(sqlite3_stmt stmt, int colIndex)
        {
            var type = raw.sqlite3_column_type(stmt, colIndex);

            return type switch
            {
                raw.SQLITE_NULL => "NULL",
                raw.SQLITE_INTEGER => raw.sqlite3_column_int64(stmt, colIndex).ToString(CultureInfo.InvariantCulture),
                raw.SQLITE_FLOAT => raw.sqlite3_column_double(stmt, colIndex).ToString(CultureInfo.InvariantCulture),
                raw.SQLITE_TEXT => raw.sqlite3_column_text(stmt, colIndex).utf8_to_string() ?? "",
                raw.SQLITE_BLOB => $"[BLOB {raw.sqlite3_column_bytes(stmt, colIndex)} bytes]",
                _ => ""
            };
        }

        private async Task<TimeSpan> GetReportQueryTimeoutAsync()
        {
            if (_settings == null)
                return ReportSqlGuard.DefaultTimeout;

            var settings = await _settings.GetSettingsAsync();
            var timeoutMilliseconds = settings
                .FirstOrDefault(x => x.SettingKey == SettingKeys.ReportQueryTimeoutMilliseconds)
                ?.IntValue
                ?? ReportSqlGuard.DefaultTimeoutMilliseconds;

            return ReportSqlGuard.NormalizeTimeoutMilliseconds(timeoutMilliseconds);
        }

        private static void ThrowIfTimedOut(Stopwatch stopwatch, TimeSpan timeout)
        {
            if (stopwatch.Elapsed > timeout)
                throw CreateTimeoutException(timeout);
        }

        private static TimeoutException CreateTimeoutException(TimeSpan timeout)
        {
            return new TimeoutException($"Report execution exceeded {ToMilliseconds(timeout)} milliseconds.");
        }

        private static int ToMilliseconds(TimeSpan timeout)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(1, timeout.TotalMilliseconds));
        }

        private ReportModel MapReportRow(ReportRow row)
        {
            return new ReportModel
            {
                Id = row.Id,
                Title = row.Title,
                SQLQuery = row.SQLQuery,
                LastRunOn = FromDbDateTime(row.LastRunOn),
                EligibleForAchievment = row.EligibleForAchievment == 1
            };
        }

        private sealed class IdRow
        {
            public int Id { get; set; }
        }

        private sealed class ReportRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string SQLQuery { get; set; } = "";
            public string? LastRunOn { get; set; }
            public int EligibleForAchievment { get; set; }
        }
    }
}
