using System.Globalization;
using Points.Services.Scheduling;
using Points.Services.Time;
using SQLite;

namespace Points.Services.Sqlite;

internal sealed class TimeHandlingDatabaseMigration
{
    private const string MigrationKey = "2026-04-time-handling-normalization-v1";

    private static readonly string[] LocalTimeFormats =
    {
        StrictTimeSerializer.LocalTimeFormat,
        "HH:mm",
        "H:mm:ss",
        "H:mm"
    };

    private readonly SQLiteAsyncConnection _db;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IClock _clock;

    public TimeHandlingDatabaseMigration(
        SQLiteAsyncConnection db,
        ITimeZoneService timeZoneService,
        IClock clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task RunAsync()
    {
        await _db.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS SchemaMigration (
                MigrationKey TEXT PRIMARY KEY,
                AppliedAtUtc TEXT NOT NULL
            );
        ");

        var alreadyApplied = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SchemaMigration WHERE MigrationKey = ?;",
            MigrationKey);

        if (alreadyApplied > 0)
            return;

        await _db.RunInTransactionAsync(conn =>
        {
            NormalizeInstantRangeColumn(conn, "Activity", "Start", "End");

            NormalizeInstantColumn(conn, "ScCardStepRep", "TimeStamp", ignoreConflicts: true);
            NormalizeInstantColumn(conn, "TrackerValue", "TimeStamp");
            NormalizeInstantColumn(conn, "BudgetCardTransaction", "TimeStamp");
            NormalizeInstantColumn(conn, "MissionCard", "CreatedDate");
            NormalizeInstantColumn(conn, "MissionCard", "CompletedDate");
            NormalizeInstantColumn(conn, "AchievementCard", "CreatedDate");
            NormalizeInstantColumn(conn, "AchievementCard", "LastEarnedAt");
            NormalizeInstantColumn(conn, "AchievementCard", "FinalizedAt");
            NormalizeInstantColumn(conn, "AchievementTrophy", "EarnedOn");
            NormalizeInstantColumn(conn, "NotificationLog", "CreatedAt");
            NormalizeInstantColumn(conn, "NotificationLog", "ScheduledAt");
            NormalizeInstantColumn(conn, "NotificationLog", "ScheduleFor", ignoreConflicts: true);
            NormalizeInstantColumn(conn, "NotificationLog", "SentAt");
            NormalizeInstantColumn(conn, "NotificationLog", "UpdatedAt");
            NormalizeInstantColumn(conn, "Planner", "CreatedAt");
            NormalizeInstantColumn(conn, "Planner", "UpdatedAt");
            NormalizeInstantColumn(conn, "Report", "LastRunOn");

            NormalizeLocalDateColumn(conn, "Planner", "PlannerDate", ignoreConflicts: true);

            NormalizeLocalDateTimeColumn(conn, "BudgetCard", "StartDate");
            NormalizeLocalDateTimeColumn(conn, "ValueTrackerCard", "CreatedDate");
            NormalizeLocalDateTimeColumn(conn, "ValueTrackerCard", "RangeStart");
            NormalizeLocalDateTimeColumn(conn, "EventTrackerCard", "CreatedDate");
            NormalizeLocalDateTimeColumn(conn, "EventTrackerCard", "RangeStart");
            NormalizeLocalDateTimeColumn(conn, "MissionCard", "AvailableFromDate");
            NormalizeLocalDateTimeColumn(conn, "MissionCard", "DueDate");
            NormalizeLocalDateTimeColumn(conn, "MissionCard", "EventDate");
            NormalizeLocalDateTimeColumn(conn, "AchievementCard", "DeadlineStart");
            NormalizeLocalDateTimeColumn(conn, "AchievementCard", "Deadline");
            NormalizeLocalDateTimeColumn(conn, "CardSchedule", "FromDateTime");
            NormalizeLocalDateTimeColumn(conn, "CardSchedule", "ToDateTime");
            NormalizeLocalDateTimeColumn(conn, "LockSchedule", "FromDateTime");
            NormalizeLocalDateTimeColumn(conn, "LockSchedule", "ToDateTime");
            NormalizeLocalDateTimeRangeColumn(conn, "PlannerTask", "PlannedStart", "PlannedEnd");
            NormalizeLocalDateTimeColumn(conn, "PlannerEvent", "PlannedTime");

            NormalizeLocalTimeColumn(conn, "Lock", "TimeWindowStart");
            NormalizeLocalTimeColumn(conn, "Lock", "TimeWindowEnd");
            NormalizeLocalTimeColumn(conn, "Goal", "DeFactoStart");
            NormalizeLocalTimeColumn(conn, "Goal", "DeFactoEnd");

            conn.Execute(
                @"INSERT OR REPLACE INTO SchemaMigration (MigrationKey, AppliedAtUtc)
                  VALUES (?, ?);",
                MigrationKey,
                StrictTimeSerializer.SerializeUtcInstant(_clock.UtcNow));
        });
    }

    private void NormalizeInstantColumn(SQLiteConnection conn, string tableName, string columnName, bool ignoreConflicts = false)
    {
        NormalizeSingleColumn(
            conn,
            tableName,
            columnName,
            TryNormalizeInstantText,
            ignoreConflicts);
    }

    private void NormalizeLocalDateColumn(SQLiteConnection conn, string tableName, string columnName, bool ignoreConflicts = false)
    {
        NormalizeSingleColumn(
            conn,
            tableName,
            columnName,
            TryNormalizeLocalDateText,
            ignoreConflicts);
    }

    private void NormalizeLocalDateTimeColumn(SQLiteConnection conn, string tableName, string columnName, bool ignoreConflicts = false)
    {
        NormalizeSingleColumn(
            conn,
            tableName,
            columnName,
            TryNormalizeLocalDateTimeText,
            ignoreConflicts);
    }

    private void NormalizeLocalTimeColumn(SQLiteConnection conn, string tableName, string columnName, bool ignoreConflicts = false)
    {
        NormalizeSingleColumn(
            conn,
            tableName,
            columnName,
            TryNormalizeLocalTimeText,
            ignoreConflicts);
    }

    private void NormalizeSingleColumn(
        SQLiteConnection conn,
        string tableName,
        string columnName,
        TryNormalizeText normalize,
        bool ignoreConflicts)
    {
        if (!ColumnExists(conn, tableName, columnName))
            return;

        var table = QuoteIdentifier(tableName);
        var column = QuoteIdentifier(columnName);
        var rows = conn.Query<SingleColumnRow>(
            $@"SELECT rowid AS RowId, {column} AS Value
               FROM {table}
               WHERE {column} IS NOT NULL
                 AND TRIM({column}) <> '';");

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Value))
                continue;

            if (!normalize(row.Value, out var normalized))
                continue;

            if (string.Equals(row.Value, normalized, StringComparison.Ordinal))
                continue;

            UpdateColumnByRowId(conn, tableName, columnName, row.RowId, normalized, ignoreConflicts);
        }
    }

    private void NormalizeInstantRangeColumn(SQLiteConnection conn, string tableName, string startColumnName, string endColumnName)
    {
        if (!ColumnExists(conn, tableName, startColumnName) || !ColumnExists(conn, tableName, endColumnName))
            return;

        var table = QuoteIdentifier(tableName);
        var startColumn = QuoteIdentifier(startColumnName);
        var endColumn = QuoteIdentifier(endColumnName);
        var rows = conn.Query<RangeColumnRow>(
            $@"SELECT rowid AS RowId, {startColumn} AS StartValue, {endColumn} AS EndValue
               FROM {table}
               WHERE {startColumn} IS NOT NULL
                 AND TRIM({startColumn}) <> '';");

        foreach (var row in rows)
        {
            if (!TryNormalizeInstantText(row.StartValue, out var normalizedStart, out var startUtc))
                continue;

            string? normalizedEnd = null;
            DateTime? endUtc = null;

            if (!string.IsNullOrWhiteSpace(row.EndValue))
            {
                if (!TryNormalizeInstantText(row.EndValue!, out normalizedEnd, out var parsedEndUtc))
                    continue;

                endUtc = parsedEndUtc;
                if (endUtc.Value <= startUtc)
                    continue;
            }

            var startChanged = !string.Equals(row.StartValue, normalizedStart, StringComparison.Ordinal);
            var endChanged = !string.Equals(row.EndValue, normalizedEnd, StringComparison.Ordinal);
            if (!startChanged && !endChanged)
                continue;

            TryExecute(
                conn,
                $@"UPDATE {table}
                   SET {startColumn} = ?, {endColumn} = ?
                   WHERE rowid = ?;",
                normalizedStart,
                normalizedEnd,
                row.RowId);
        }
    }

    private void NormalizeLocalDateTimeRangeColumn(SQLiteConnection conn, string tableName, string startColumnName, string endColumnName)
    {
        if (!ColumnExists(conn, tableName, startColumnName) || !ColumnExists(conn, tableName, endColumnName))
            return;

        var table = QuoteIdentifier(tableName);
        var startColumn = QuoteIdentifier(startColumnName);
        var endColumn = QuoteIdentifier(endColumnName);
        var rows = conn.Query<RangeColumnRow>(
            $@"SELECT rowid AS RowId, {startColumn} AS StartValue, {endColumn} AS EndValue
               FROM {table}
               WHERE {startColumn} IS NOT NULL
                 AND TRIM({startColumn}) <> ''
                 AND {endColumn} IS NOT NULL
                 AND TRIM({endColumn}) <> '';");

        foreach (var row in rows)
        {
            if (!TryNormalizeLocalDateTimeText(row.StartValue, out var normalizedStart, out var startLocal))
                continue;

            if (!TryNormalizeLocalDateTimeText(row.EndValue!, out var normalizedEnd, out var endLocal))
                continue;

            if (endLocal <= startLocal)
                continue;

            var startChanged = !string.Equals(row.StartValue, normalizedStart, StringComparison.Ordinal);
            var endChanged = !string.Equals(row.EndValue, normalizedEnd, StringComparison.Ordinal);
            if (!startChanged && !endChanged)
                continue;

            TryExecute(
                conn,
                $@"UPDATE {table}
                   SET {startColumn} = ?, {endColumn} = ?
                   WHERE rowid = ?;",
                normalizedStart,
                normalizedEnd,
                row.RowId);
        }
    }

    private bool TryNormalizeInstantText(string value, out string normalized)
    {
        if (TryNormalizeInstantText(value, out normalized, out _))
            return true;

        normalized = value;
        return false;
    }

    private bool TryNormalizeInstantText(string value, out string normalized, out DateTime utcInstant)
    {
        normalized = value;
        utcInstant = default;

        try
        {
            utcInstant = LegacyTimeReader.ReadInstantUtc(value, _timeZoneService).UtcInstant;
            normalized = StrictTimeSerializer.SerializeUtcInstant(utcInstant);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeLocalDateText(string value, out string normalized)
    {
        normalized = value;

        try
        {
            if (StrictTimeSerializer.TryParseLocalDate(value, out var localDate))
            {
                normalized = StrictTimeSerializer.SerializeLocalDate(localDate);
                return true;
            }

            var localDateTime = LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime;
            normalized = StrictTimeSerializer.SerializeLocalDate(localDateTime);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeLocalDateTimeText(string value, out string normalized)
    {
        if (TryNormalizeLocalDateTimeText(value, out normalized, out _))
            return true;

        normalized = value;
        return false;
    }

    private static bool TryNormalizeLocalDateTimeText(string value, out string normalized, out DateTime localDateTime)
    {
        normalized = value;
        localDateTime = default;

        try
        {
            localDateTime = WallClockScheduleTime.NormalizeLocal(
                LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime);
            normalized = StrictTimeSerializer.SerializeLocalDateTime(localDateTime);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeLocalTimeText(string value, out string normalized)
    {
        normalized = value;

        try
        {
            var trimmed = value.Trim();
            foreach (var format in LocalTimeFormats)
            {
                if (TimeOnly.TryParseExact(
                        trimmed,
                        format,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var exactTime))
                {
                    normalized = StrictTimeSerializer.SerializeLocalTime(exactTime);
                    return true;
                }
            }

            if (TimeOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
            {
                normalized = StrictTimeSerializer.SerializeLocalTime(parsedTime);
                return true;
            }

            var localDateTime = LegacyTimeReader.ReadLocalDateTime(trimmed).LocalDateTime;
            normalized = StrictTimeSerializer.SerializeLocalTime(TimeOnly.FromDateTime(localDateTime));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void UpdateColumnByRowId(
        SQLiteConnection conn,
        string tableName,
        string columnName,
        long rowId,
        string normalized,
        bool ignoreConflicts)
    {
        var updateVerb = ignoreConflicts ? "UPDATE OR IGNORE" : "UPDATE";

        TryExecute(
            conn,
            $@"{updateVerb} {QuoteIdentifier(tableName)}
               SET {QuoteIdentifier(columnName)} = ?
               WHERE rowid = ?;",
            normalized,
            rowId);
    }

    private static bool ColumnExists(SQLiteConnection conn, string tableName, string columnName)
    {
        if (!TableExists(conn, tableName))
            return false;

        var columns = conn.Query<TableInfoRow>($"PRAGMA table_info({QuoteIdentifier(tableName)});");
        return columns.Any(c => string.Equals(c.name, columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TableExists(SQLiteConnection conn, string tableName)
    {
        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?;",
            tableName);

        return count > 0;
    }

    private static void TryExecute(SQLiteConnection conn, string sql, params object?[] args)
    {
        try
        {
            conn.Execute(sql, args);
        }
        catch (SQLiteException)
        {
            // Leave unparseable or constraint-conflicting legacy rows untouched.
            // LegacyTimeReader remains in place for compatibility on reads.
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private delegate bool TryNormalizeText(string value, out string normalized);

    private sealed class SingleColumnRow
    {
        public long RowId { get; set; }
        public string? Value { get; set; }
    }

    private sealed class RangeColumnRow
    {
        public long RowId { get; set; }
        public string StartValue { get; set; } = "";
        public string? EndValue { get; set; }
    }

    private sealed class TableInfoRow
    {
        public string name { get; set; } = "";
    }
}
