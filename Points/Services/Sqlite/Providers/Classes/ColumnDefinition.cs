namespace Points.Services.Sqlite.Providers.Classes
{
    public sealed class ColumnDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string SqlType { get; init; } = string.Empty;
        public bool IsPrimaryKey { get; init; }
        public bool IsAutoIncrement { get; init; }
        public bool IsNullable { get; init; } = true;
        public string? DefaultSql { get; init; }
    }
}
