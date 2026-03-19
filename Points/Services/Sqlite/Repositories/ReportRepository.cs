using System.Globalization;
using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Repositories.Interfaces;
using SQLitePCL;
using static SQLitePCL.raw;

namespace Points.Services.Sqlite
{
    public sealed partial class ReportRepository : SqliteRepositoryBase, IReportRepository
    {
        private readonly string _dbPath;

        public ReportRepository(ISqliteConnectionManager connectionManager, string dbPath) : base(connectionManager)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("Database path is required.", nameof(dbPath));

            _dbPath = dbPath;
        }

        public async Task<IReadOnlyList<string>> ExecuteSelectForReportAsync(
            string sql,
            bool includeHeaderRow = true,
            params object?[] args)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(sql))
                return Array.Empty<string>();

            var trimmed = sql.TrimStart();
            if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only SELECT statements are allowed.");
            }

            return await Task.Run(() =>
            {
                sqlite3? db = null;
                sqlite3_stmt? stmt = null;

                try
                {
                    var rc = sqlite3_open_v2(
                        _dbPath,
                        out db,
                        SQLITE_OPEN_READONLY,
                        null);

                    if (rc != SQLITE_OK || db == null)
                        throw new InvalidOperationException($"Failed to open SQLite database. rc={rc}");

                    rc = sqlite3_prepare_v2(db, sql, out stmt);
                    if (rc != SQLITE_OK || stmt == null)
                    {
                        throw new InvalidOperationException(
                            $"sqlite3_prepare_v2 failed. rc={rc}. {sqlite3_errmsg(db).utf8_to_string()}");
                    }

                    if (args is { Length: > 0 })
                    {
                        for (int i = 0; i < args.Length; i++)
                        {
                            BindParameter(stmt, i + 1, args[i]);
                        }
                    }

                    var results = new List<string>();
                    var colCount = sqlite3_column_count(stmt);

                    if (includeHeaderRow && colCount > 0)
                    {
                        var headers = new string[colCount];
                        for (int c = 0; c < colCount; c++)
                        {
                            var name = sqlite3_column_name(stmt, c).utf8_to_string();
                            headers[c] = string.IsNullOrWhiteSpace(name) ? $"Col{c + 1}" : name;
                        }

                        results.Add(string.Join(" | ", headers));
                    }

                    while (true)
                    {
                        rc = sqlite3_step(stmt);

                        if (rc == SQLITE_ROW)
                        {
                            var row = new string[colCount];
                            for (int c = 0; c < colCount; c++)
                            {
                                row[c] = ReadColumnAsText(stmt, c);
                            }

                            results.Add(string.Join(" | ", row));
                            continue;
                        }

                        if (rc == SQLITE_DONE)
                            break;

                        throw new InvalidOperationException(
                            $"sqlite3_step failed. rc={rc}. {sqlite3_errmsg(db).utf8_to_string()}");
                    }

                    if (results.Count == 0)
                        results.Add("(no rows)");

                    return (IReadOnlyList<string>)results;
                }
                finally
                {
                    if (stmt != null)
                        sqlite3_finalize(stmt);

                    if (db != null)
                        sqlite3_close(db);
                }
            }).ConfigureAwait(false);
        }

        public async Task UpsertReportAsync(ReportModel report)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(report);

            if (string.IsNullOrWhiteSpace(report.Title))
                throw new ArgumentException("Report.Title is required.", nameof(report));

            await Db.RunInTransactionAsync(conn =>
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
                    @"SELECT Id
                      FROM Report
                      WHERE Title = ?
                      LIMIT 1;",
                    report.Title).FirstOrDefault();

                if (idRow != null)
                    report.Id = idRow.Id;
            }).ConfigureAwait(false);
        }

        public async Task DeleteReportAsync(int reportId)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            await Db.RunInTransactionAsync(conn =>
            {
                conn.Execute("DELETE FROM Report WHERE Id = ?;", reportId);
            }).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ReportModel>> GetReportsAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            const string sql = @"
                SELECT
                    r.Id                    AS Id,
                    r.Title                 AS Title,
                    r.SQLQuery              AS SQLQuery,
                    r.LastRunOn             AS LastRunOn,
                    r.EligibleForAchievment AS EligibleForAchievment
                FROM Report r
                ORDER BY r.Title;";

            var rows = await Db.QueryAsync<ReportRow>(sql).ConfigureAwait(false);
            return rows.Select(MapReportRow).ToList();
        }

        private static string? ToDbDateTime(DateTime? dt)
            => dt?.ToString("o", CultureInfo.InvariantCulture);

        private static DateTime? FromDbDateTime(string? s)
            => string.IsNullOrWhiteSpace(s)
                ? null
                : DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        private static ReportModel MapReportRow(ReportRow row)
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

        private static void BindParameter(sqlite3_stmt stmt, int index, object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                sqlite3_bind_null(stmt, index);
                return;
            }

            switch (value)
            {
                case string s:
                    sqlite3_bind_text(stmt, index, s);
                    return;

                case char ch:
                    sqlite3_bind_text(stmt, index, ch.ToString());
                    return;

                case bool b:
                    sqlite3_bind_int(stmt, index, b ? 1 : 0);
                    return;

                case byte v:
                    sqlite3_bind_int(stmt, index, v);
                    return;

                case short v:
                    sqlite3_bind_int(stmt, index, v);
                    return;

                case int v:
                    sqlite3_bind_int(stmt, index, v);
                    return;

                case long v:
                    sqlite3_bind_int64(stmt, index, v);
                    return;

                case sbyte v:
                    sqlite3_bind_int(stmt, index, v);
                    return;

                case ushort v:
                    sqlite3_bind_int(stmt, index, v);
                    return;

                case uint v:
                    sqlite3_bind_int64(stmt, index, v);
                    return;

                case ulong v:
                    if (v > long.MaxValue)
                        throw new InvalidOperationException($"Parameter {index} is too large for SQLite signed INTEGER.");

                    sqlite3_bind_int64(stmt, index, (long)v);
                    return;

                case float v:
                    sqlite3_bind_double(stmt, index, v);
                    return;

                case double v:
                    sqlite3_bind_double(stmt, index, v);
                    return;

                case decimal v:
                    sqlite3_bind_text(stmt, index, v.ToString(CultureInfo.InvariantCulture));
                    return;

                case DateTime dt:
                    sqlite3_bind_text(stmt, index, dt.ToString("o", CultureInfo.InvariantCulture));
                    return;

                case DateTimeOffset dto:
                    sqlite3_bind_text(stmt, index, dto.ToString("o", CultureInfo.InvariantCulture));
                    return;

                case Guid guid:
                    sqlite3_bind_text(stmt, index, guid.ToString());
                    return;

                case byte[] bytes:
                    sqlite3_bind_blob(stmt, index, bytes);
                    return;

                default:
                    sqlite3_bind_text(stmt, index, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                    return;
            }
        }

        private static string ReadColumnAsText(sqlite3_stmt stmt, int columnIndex)
        {
            var type = sqlite3_column_type(stmt, columnIndex);

            return type switch
            {
                SQLITE_NULL => string.Empty,
                SQLITE_INTEGER => sqlite3_column_int64(stmt, columnIndex).ToString(CultureInfo.InvariantCulture),
                SQLITE_FLOAT => sqlite3_column_double(stmt, columnIndex).ToString(CultureInfo.InvariantCulture),
                SQLITE_TEXT => sqlite3_column_text(stmt, columnIndex).utf8_to_string() ?? string.Empty,
                SQLITE_BLOB => Convert.ToBase64String(sqlite3_column_blob(stmt, columnIndex) ?? Array.Empty<byte>()),
                _ => string.Empty
            };
        }
    }
}