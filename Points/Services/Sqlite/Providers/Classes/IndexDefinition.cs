namespace Points.Services.Sqlite.Providers.Classes
{
    public sealed class IndexDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string TableName { get; init; } = string.Empty;
        public bool IsUnique { get; init; }
        public List<string> Columns { get; init; } = new();
        public string? WhereSql { get; init; }

        /// <summary>
        /// When true, Columns are emitted as raw SQL expressions rather than escaped identifiers.
        /// Needed for indexes like ON Activity(1) WHERE "End" IS NULL.
        /// </summary>
        public bool TreatColumnsAsExpressions { get; init; }
    }
}
