using SQLite;

namespace Points.Services.Sqlite;

internal static class MissionCardSchemaMigration
{
    public static async Task EnsureAsync(SQLiteAsyncConnection db)
    {
        if (db == null)
            throw new ArgumentNullException(nameof(db));

        await AddColumnIfMissingAsync(db, "MissionCard", "MissionGuid", "TEXT NULL");
        await AddColumnIfMissingAsync(db, "MissionCard", "SharedWith", "TEXT NULL");
        await BackfillMissionGuidsAsync(db);
        await db.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS UX_MissionCard_MissionGuid ON MissionCard(MissionGuid);");
    }

    private static async Task AddColumnIfMissingAsync(
        SQLiteAsyncConnection db,
        string tableName,
        string columnName,
        string definition)
    {
        var cols = await db.QueryAsync<PragmaTableInfo>($"PRAGMA table_info({tableName});");
        var existing = cols
            .Select(c => c.name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (existing.Contains(columnName))
            return;

        await db.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
    }

    private static async Task BackfillMissionGuidsAsync(SQLiteAsyncConnection db)
    {
        var rows = await db.QueryAsync<MissionCardIdRow>(
            @"SELECT MissionCardID
              FROM MissionCard
              WHERE MissionGuid IS NULL
                 OR TRIM(MissionGuid) = '';");

        foreach (var row in rows)
        {
            await db.ExecuteAsync(
                "UPDATE MissionCard SET MissionGuid = ? WHERE MissionCardID = ?;",
                Guid.NewGuid().ToString("D"),
                row.MissionCardID);
        }
    }

    private sealed class PragmaTableInfo
    {
        public string name { get; set; } = "";
    }

    private sealed class MissionCardIdRow
    {
        public long MissionCardID { get; set; }
    }
}
