using System.Text;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Providers.Interfaces;
using SQLite;

namespace Points.Services.Sqlite
{
    public sealed class SqliteSchemaManager : ISqliteSchemaManager
    {
        private readonly IPointsSchemaProvider _schemaProvider;
        private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
        private bool _schemaEnsured;

        public SqliteSchemaManager(IPointsSchemaProvider schemaProvider)
        {
            _schemaProvider = schemaProvider ?? throw new ArgumentNullException(nameof(schemaProvider));
        }

        public async Task EnsureSchemaAsync(SQLiteAsyncConnection db)
        {
            ArgumentNullException.ThrowIfNull(db);

            if (_schemaEnsured)
                return;

            await _schemaSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_schemaEnsured)
                    return;

                await db.ExecuteAsync("PRAGMA foreign_keys = ON;").ConfigureAwait(false);

                var schema = _schemaProvider.GetSchema()
                    ?? throw new InvalidOperationException("Schema provider returned null.");

                await EnsureSchemaHistoryTableAsync(db).ConfigureAwait(false);

                foreach (var table in schema.Tables)
                {
                    await EnsureTableAsync(db, table).ConfigureAwait(false);
                }

                foreach (var index in schema.Indexes)
                {
                    await EnsureIndexAsync(db, index).ConfigureAwait(false);
                }

                _schemaEnsured = true;
            }
            finally
            {
                _schemaSemaphore.Release();
            }
        }

        private static async Task EnsureSchemaHistoryTableAsync(SQLiteAsyncConnection db)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS SchemaHistory
                (
                    Version     INTEGER PRIMARY KEY,
                    AppliedOn   TEXT NOT NULL,
                    Description TEXT NOT NULL
                );";

            await db.ExecuteAsync(sql).ConfigureAwait(false);
        }

        private static async Task EnsureTableAsync(SQLiteAsyncConnection db, TableDefinition expectedTable)
        {
            ValidateTableDefinition(expectedTable);

            var tableExists = await TableExistsAsync(db, expectedTable.Name).ConfigureAwait(false);
            if (!tableExists)
            {
                var createSql = BuildCreateTableSql(expectedTable);
                await db.ExecuteAsync(createSql).ConfigureAwait(false);
                return;
            }

            var actualColumns = await GetTableColumnsAsync(db, expectedTable.Name).ConfigureAwait(false);
            var actualByName = actualColumns.ToDictionary(x => x.name, StringComparer.OrdinalIgnoreCase);

            foreach (var expectedColumn in expectedTable.Columns)
            {
                if (!actualByName.TryGetValue(expectedColumn.Name, out var actualColumn))
                {
                    var addSql = BuildAddColumnSql(expectedTable.Name, expectedColumn);
                    await db.ExecuteAsync(addSql).ConfigureAwait(false);
                    continue;
                }

                ValidateCompatibleColumnShape(expectedTable.Name, expectedColumn, actualColumn);
            }
        }

        private static async Task EnsureIndexAsync(SQLiteAsyncConnection db, IndexDefinition index)
        {
            ValidateIndexDefinition(index);

            var exists = await IndexExistsAsync(db, index.Name).ConfigureAwait(false);
            if (exists)
                return;

            var sql = BuildCreateIndexSql(index);
            await db.ExecuteAsync(sql).ConfigureAwait(false);
        }

        private static void ValidateTableDefinition(TableDefinition table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            if (string.IsNullOrWhiteSpace(table.Name))
                throw new InvalidOperationException("TableDefinition.Name is required.");

            if (table.Columns == null || table.Columns.Count == 0)
                throw new InvalidOperationException($"Table '{table.Name}' must define at least one column.");

            var duplicateNames = table.Columns
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateNames.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Table '{table.Name}' defines duplicate column(s): {string.Join(", ", duplicateNames)}");
            }

            var pkAutoIncrementCount = table.Columns.Count(c => c.IsAutoIncrement);
            if (pkAutoIncrementCount > 1)
            {
                throw new InvalidOperationException(
                    $"Table '{table.Name}' defines more than one AUTOINCREMENT column.");
            }

            if (table.Columns.Any(c => c.IsAutoIncrement && !c.IsPrimaryKey))
            {
                throw new InvalidOperationException(
                    $"Table '{table.Name}' defines AUTOINCREMENT on a non-primary-key column.");
            }
        }

        private static void ValidateIndexDefinition(IndexDefinition index)
        {
            if (index == null)
                throw new ArgumentNullException(nameof(index));

            if (string.IsNullOrWhiteSpace(index.Name))
                throw new InvalidOperationException("IndexDefinition.Name is required.");

            if (string.IsNullOrWhiteSpace(index.TableName))
                throw new InvalidOperationException($"Index '{index.Name}' must define TableName.");

            if (index.Columns == null || index.Columns.Count == 0)
                throw new InvalidOperationException($"Index '{index.Name}' must define at least one column.");
        }

        private static void ValidateCompatibleColumnShape(
            string tableName,
            ColumnDefinition expected,
            PragmaTableInfo actual)
        {
            var expectedType = NormalizeSqlType(expected.SqlType);
            var actualType = NormalizeSqlType(actual.type);

            if (!string.Equals(expectedType, actualType, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"Schema mismatch in table '{tableName}', column '{expected.Name}': " +
                    $"expected type '{expected.SqlType}', found '{actual.type}'. " +
                    "This change requires a rebuild migration, which SqliteSchemaManager does not perform yet.");
            }

            var expectedNotNull = !expected.IsNullable || expected.IsPrimaryKey;
            var actualNotNull = actual.notnull != 0;

            if (expectedNotNull != actualNotNull)
            {
                throw new NotSupportedException(
                    $"Schema mismatch in table '{tableName}', column '{expected.Name}': " +
                    $"expected NOT NULL = {expectedNotNull}, found NOT NULL = {actualNotNull}. " +
                    "This change requires a rebuild migration, which SqliteSchemaManager does not perform yet.");
            }

            var expectedPk = expected.IsPrimaryKey;
            var actualPk = actual.pk != 0;

            if (expectedPk != actualPk)
            {
                throw new NotSupportedException(
                    $"Schema mismatch in table '{tableName}', column '{expected.Name}': " +
                    $"expected PK = {expectedPk}, found PK = {actualPk}. " +
                    "This change requires a rebuild migration, which SqliteSchemaManager does not perform yet.");
            }
        }

        private static string BuildCreateTableSql(TableDefinition table)
        {
            var parts = new List<string>();

            foreach (var column in table.Columns)
            {
                parts.Add(BuildColumnDefinitionSql(column, forCreateTable: true));
            }

            if (table.TableConstraints != null && table.TableConstraints.Count > 0)
            {
                parts.AddRange(table.TableConstraints.Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            return $@"
CREATE TABLE IF NOT EXISTS {EscapeIdentifier(table.Name)}
(
    {string.Join(",\n    ", parts)}
);";
        }

        private static string BuildAddColumnSql(string tableName, ColumnDefinition column)
        {
            if (column.IsPrimaryKey || column.IsAutoIncrement)
            {
                throw new NotSupportedException(
                    $"Cannot add primary key / autoincrement column '{column.Name}' to existing table '{tableName}' " +
                    "with ALTER TABLE. This requires a rebuild migration.");
            }

            if (!column.IsNullable && string.IsNullOrWhiteSpace(column.DefaultSql))
            {
                throw new NotSupportedException(
                    $"Cannot add NOT NULL column '{column.Name}' to existing table '{tableName}' without a default value. " +
                    "This requires a rebuild migration or an explicit default.");
            }

            return $@"ALTER TABLE {EscapeIdentifier(tableName)} ADD COLUMN {BuildColumnDefinitionSql(column, forCreateTable: false)};";
        }

        private static string BuildColumnDefinitionSql(ColumnDefinition column, bool forCreateTable)
        {
            if (column == null)
                throw new ArgumentNullException(nameof(column));

            if (string.IsNullOrWhiteSpace(column.Name))
                throw new InvalidOperationException("ColumnDefinition.Name is required.");

            if (string.IsNullOrWhiteSpace(column.SqlType))
                throw new InvalidOperationException($"Column '{column.Name}' must define SqlType.");

            var sb = new StringBuilder();
            sb.Append(EscapeIdentifier(column.Name));
            sb.Append(' ');
            sb.Append(column.SqlType);

            if (column.IsPrimaryKey)
            {
                sb.Append(" PRIMARY KEY");
                if (column.IsAutoIncrement)
                    sb.Append(" AUTOINCREMENT");
            }

            if (!column.IsNullable || column.IsPrimaryKey)
                sb.Append(" NOT NULL");

            if (!string.IsNullOrWhiteSpace(column.DefaultSql))
            {
                sb.Append(" DEFAULT ");
                sb.Append(column.DefaultSql);
            }

            return sb.ToString();
        }

        private static string BuildCreateIndexSql(IndexDefinition index)
        {
            var uniqueness = index.IsUnique ? "UNIQUE " : string.Empty;
            var columns = string.Join(", ", index.Columns.Select(EscapeIdentifier));

            var sql = new StringBuilder();
            sql.Append("CREATE ");
            sql.Append(uniqueness);
            sql.Append("INDEX IF NOT EXISTS ");
            sql.Append(EscapeIdentifier(index.Name));
            sql.Append(" ON ");
            sql.Append(EscapeIdentifier(index.TableName));
            sql.Append('(');
            sql.Append(columns);
            sql.Append(')');

            if (!string.IsNullOrWhiteSpace(index.WhereSql))
            {
                sql.Append(" WHERE ");
                sql.Append(index.WhereSql);
            }

            sql.Append(';');
            return sql.ToString();
        }

        private static async Task<bool> TableExistsAsync(SQLiteAsyncConnection db, string tableName)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = ?;";

            var count = await db.ExecuteScalarAsync<int>(sql, tableName).ConfigureAwait(false);
            return count > 0;
        }

        private static async Task<bool> IndexExistsAsync(SQLiteAsyncConnection db, string indexName)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'index'
                  AND name = ?;";

            var count = await db.ExecuteScalarAsync<int>(sql, indexName).ConfigureAwait(false);
            return count > 0;
        }

        private static async Task<List<PragmaTableInfo>> GetTableColumnsAsync(SQLiteAsyncConnection db, string tableName)
        {
            var sql = $"PRAGMA table_info({EscapeIdentifier(tableName)});";
            return await db.QueryAsync<PragmaTableInfo>(sql).ConfigureAwait(false);
        }

        private static string NormalizeSqlType(string? type)
        {
            return (type ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string EscapeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier is required.", nameof(identifier));

            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        private sealed class PragmaTableInfo
        {
            public int cid { get; set; }
            public string name { get; set; } = string.Empty;
            public string type { get; set; } = string.Empty;
            public int notnull { get; set; }
            public string? dflt_value { get; set; }
            public int pk { get; set; }
        }
    }
}