namespace Points.Services.Sqlite.Providers.Classes
{
    public sealed class TableDefinition
    {
        public string Name { get; init; } = string.Empty;
        public List<ColumnDefinition> Columns { get; init; } = new();
        public List<string> TableConstraints { get; init; } = new();
    }
}
