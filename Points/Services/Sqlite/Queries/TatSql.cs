namespace Points.Services.Sqlite.Queries
{
    internal static class TatSql
    {
        public const string GetTatModelDataById = @"
                SELECT
                    t.TatCardID      AS TatCardID,
                    t.CardID         AS CardID,

                    c.Title          AS Title,
                    c.Tags           AS Tags,

                    t.ValuePerMinute AS ValuePerMinute,
                    t.Status         AS Status,
                    t.Description    AS Description,
                    t.TargetActiveTimeSeconds AS TargetActiveTimeSeconds
                FROM TatCard t
                JOIN Card c ON c.CardID = t.CardID
                WHERE t.TatCardID = ?
                LIMIT 1;
            ";
    }
}
