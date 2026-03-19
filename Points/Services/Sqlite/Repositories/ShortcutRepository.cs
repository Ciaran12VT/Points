using Points.Models;
using Points.Services.Sqlite.Managers.Interfaces;
using Points.Services.Sqlite.Repositories.Interfaces;

namespace Points.Services.Sqlite
{
    public sealed partial class ShortcutRepository : SqliteRepositoryBase, IShortcutRepository
    {
        public ShortcutRepository(ISqliteConnectionManager connectionManager) : base(connectionManager)
        {
        }

        public async Task<List<ShortcutGroupModel>> GetShortcutGroupsAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var rows = await Db.QueryAsync<ShortcutGroupRow>(
                @"SELECT ShortcutGroupId, Name, Color, ShortcutGroupOrder
                  FROM ShortcutGroup
                  ORDER BY ShortcutGroupOrder ASC, ShortcutGroupId ASC;")
                .ConfigureAwait(false);

            return rows.Select(ShortcutGroupMapper.ToDomain).ToList();
        }

        public async Task<List<ShortcutModel>> GetDashboardShortcutsAsync()
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            var joinRows = await Db.QueryAsync<DashboardShortcutJoinRow>(
                @"SELECT
                      s.ShortcutId         AS ShortcutId,
                      s.IconChar           AS IconChar,
                      s.TargetCardId       AS TargetCardId,
                      s.ShortcutOrder      AS ShortcutOrder,
                      g.ShortcutGroupId    AS ShortcutGroupId,
                      g.Name               AS GroupName,
                      g.Color              AS GroupColor,
                      g.ShortcutGroupOrder AS ShortcutGroupOrder
                  FROM Shortcut s
                  JOIN ShortcutGroup g ON g.ShortcutGroupId = s.ShortcutGroupId
                  ORDER BY g.ShortcutGroupOrder ASC, s.ShortcutOrder ASC, s.ShortcutId ASC;")
                .ConfigureAwait(false);

            return joinRows.Select(ShortcutMapper.ToDomain).ToList();
        }

        public async Task<ShortcutGroupModel> UpsertShortcutGroupAsync(ShortcutGroupModel group)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(group);

            if (string.IsNullOrWhiteSpace(group.Name))
                throw new ArgumentException("Group.Name is required.", nameof(group));

            var name = group.Name.Trim();
            var colorHex = NormalizeArgbHex(ToHexArgb(group.Color));
            var order = group.ShortcutGroupOrder;

            ShortcutGroupModel? result = null;

            await Db.RunInTransactionAsync(conn =>
            {
                var existing = conn.Query<ShortcutGroupRow>(
                    @"SELECT ShortcutGroupId, Name, Color, ShortcutGroupOrder
                      FROM ShortcutGroup
                      WHERE Name = ? COLLATE NOCASE
                      LIMIT 1;",
                    name)
                    .FirstOrDefault();

                if (existing != null)
                {
                    conn.Execute(
                        @"UPDATE ShortcutGroup
                          SET Color = ?, ShortcutGroupOrder = ?
                          WHERE ShortcutGroupId = ?;",
                        colorHex,
                        order,
                        existing.ShortcutGroupId);

                    result = new ShortcutGroupModel
                    {
                        ShortcutGroupId = existing.ShortcutGroupId,
                        Name = existing.Name,
                        Color = ParseColor(colorHex),
                        ShortcutGroupOrder = order
                    };

                    return;
                }

                conn.Execute(
                    @"INSERT INTO ShortcutGroup (Name, Color, ShortcutGroupOrder)
                      VALUES (?, ?, ?);",
                    name,
                    colorHex,
                    order);

                var newId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");

                result = new ShortcutGroupModel
                {
                    ShortcutGroupId = newId,
                    Name = name,
                    Color = ParseColor(colorHex),
                    ShortcutGroupOrder = order
                };
            }).ConfigureAwait(false);

            return result ?? throw new InvalidOperationException("UpsertShortcutGroupAsync failed unexpectedly.");
        }

        public async Task<ShortcutModel> SaveShortcutAsync(ShortcutModel shortcut)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            ArgumentNullException.ThrowIfNull(shortcut);

            if (shortcut.TargetCardId <= 0)
                throw new ArgumentException("TargetCardId must be set.", nameof(shortcut));

            if (shortcut.ShortcutGroupId <= 0)
                throw new ArgumentException("ShortcutGroupId must be set.", nameof(shortcut));

            shortcut.IconChar = (shortcut.IconChar ?? string.Empty).Trim();

            var row = ShortcutMapper.ToRow(shortcut);
            long savedId = row.ShortcutId;

            await Db.RunInTransactionAsync(conn =>
            {
                if (savedId <= 0)
                {
                    conn.Execute(
                        @"INSERT INTO Shortcut (IconChar, TargetCardId, ShortcutGroupId, ShortcutOrder)
                          VALUES (?, ?, ?, ?);",
                        row.IconChar,
                        row.TargetCardId,
                        row.ShortcutGroupId,
                        row.ShortcutOrder);

                    savedId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                }
                else
                {
                    conn.Execute(
                        @"UPDATE Shortcut
                          SET IconChar = ?, TargetCardId = ?, ShortcutGroupId = ?, ShortcutOrder = ?
                          WHERE ShortcutId = ?;",
                        row.IconChar,
                        row.TargetCardId,
                        row.ShortcutGroupId,
                        row.ShortcutOrder,
                        savedId);
                }
            }).ConfigureAwait(false);

            shortcut.ShortcutId = savedId;
            return shortcut;
        }

        public async Task DeleteShortcutAsync(long shortcutId)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            if (shortcutId <= 0)
                return;

            await Db.ExecuteAsync(
                @"DELETE FROM Shortcut
                  WHERE ShortcutId = ?;",
                shortcutId).ConfigureAwait(false);
        }


        #region Color Helpers

        private static Color ParseColor(string? hex)
        {
            var norm = NormalizeArgbHex(hex);
            return Color.FromArgb(norm);
        }

        private static string ToHexArgb(Color color)
        {
            byte a = (byte)Math.Round(color.Alpha * 255);
            byte r = (byte)Math.Round(color.Red * 255);
            byte g = (byte)Math.Round(color.Green * 255);
            byte b = (byte)Math.Round(color.Blue * 255);

            return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        }

        private static string NormalizeArgbHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return "#FF000000";

            hex = hex.Trim();

            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            if (hex.Length == 7)
                return "#FF" + hex[1..];

            if (hex.Length == 9)
                return hex;

            return "#FF000000";
        }

        #endregion
    }
}