using System.Globalization;
using Points.Services.Sqlite.Managers.Interfaces;
using SQLite;

namespace Points.Services.Sqlite
{
    /// <summary>
    /// Common base for SQLite repositories in V2.
    /// Keeps shared access to the connection manager, the async DB handle,
    /// and a few cross-cutting helper methods used throughout the persistence layer.
    /// </summary>
    internal abstract class SqliteRepositoryBase
    {
        protected SqliteRepositoryBase(ISqliteConnectionManager connectionManager)
        {
            ConnectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        /// <summary>
        /// Shared connection/lifecycle owner for all repositories.
        /// </summary>
        protected ISqliteConnectionManager ConnectionManager { get; }

        /// <summary>
        /// Shared sqlite-net async connection.
        /// Mirrors the old SqliteDbService Db property pattern.
        /// </summary>
        protected SQLiteAsyncConnection Db => ConnectionManager.Db;

        /// <summary>
        /// Ensures the underlying database connection has been initialized.
        /// Repositories should generally call this at the start of each public method.
        /// </summary>
        protected Task EnsureInitializedAsync() => ConnectionManager.InitializeAsync();

        #region Date / Time Helpers

        /// <summary>
        /// Stores a DateTime as ISO-8601 round-trip text.
        /// Preserves the same storage convention used throughout the existing SQLite layer.
        /// </summary>
        protected static string ToDbDateTime(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

        /// <summary>
        /// Stores a nullable DateTime as ISO-8601 round-trip text.
        /// </summary>
        protected static string? ToDbDateTime(DateTime? value) => value?.ToString("o", CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses an ISO-8601 round-trip string back to DateTime.
        /// </summary>
        protected static DateTime ParseIsoDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty ISO datetime string is required.", nameof(value));

            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        /// <summary>
        /// Parses a nullable ISO-8601 round-trip string back to DateTime?.
        /// </summary>
        protected static DateTime? ParseNullableIsoDateTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            return DateTime.Parse(value,  CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        /// <summary>
        /// Forces UTC before persisting as ISO-8601 text.
        /// Useful for rows whose semantics are explicitly UTC.
        /// </summary>
        protected static string ToUtcDbDateTime(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

            return utc.ToString("o", CultureInfo.InvariantCulture);
        }

        #endregion

        #region TimeOnly / TimeSpan Helpers

        /// <summary>
        /// Stores a TimeOnly as HH:mm:ss.
        /// Matches the current conventions used in planner/lock persistence.
        /// </summary>
        protected static string ToDbTimeOnly(TimeOnly value) => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>
        /// Stores a nullable TimeOnly as HH:mm:ss.
        /// </summary>
        protected static string? ToDbTimeOnly(TimeOnly? value) => value?.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses HH:mm:ss into TimeOnly.
        /// </summary>
        protected static TimeOnly ParseTimeOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty time string is required.", nameof(value));

            return TimeOnly.ParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses nullable HH:mm:ss into TimeOnly?.
        /// </summary>
        protected static TimeOnly? ParseNullableTimeOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            return TimeOnly.ParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Stores a TimeSpan as whole seconds from midnight.
        /// Useful for tables that persist time-of-day as seconds.
        /// </summary>
        protected static int ToDbSeconds(TimeSpan value) => (int)Math.Round(value.TotalSeconds);

        /// <summary>
        /// Parses whole seconds-from-midnight back into TimeSpan.
        /// </summary>
        protected static TimeSpan ParseSecondsToTimeSpan(int value) => TimeSpan.FromSeconds(value);

        #endregion

        #region SQL Helpers

        /// <summary>
        /// Builds a ?, ?, ? placeholder list for IN clauses.
        /// </summary>
        protected static string BuildPlaceholders(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Placeholder count must be greater than zero.");

            return string.Join(", ", Enumerable.Repeat("?", count));
        }

        /// <summary>
        /// Converts values to an object[] suitable for sqlite-net parameter arrays.
        /// </summary>
        protected static object[] ToSqlArgs<T>(IEnumerable<T> values)
        {
            if (values == null) return Array.Empty<object>();

            return values.Cast<object>().ToArray();
        }

        #endregion
    }
}