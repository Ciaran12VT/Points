namespace Points.Services.Sqlite.Providers.Classes
{
    public sealed class DatabaseSchemaDefinition
    {
        public List<TableDefinition> Tables { get; init; } = new();
        public List<IndexDefinition> Indexes { get; init; } = new();
    }
}
