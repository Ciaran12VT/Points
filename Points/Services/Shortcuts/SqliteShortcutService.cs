using Microsoft.Maui.Graphics;
using Points.Models;
using Points.Services.Sqlite.Interfaces;

namespace Points.Services.Shortcuts
{
    public sealed class SqliteShortcutService : IShortcutService
    {
        private readonly ISqliteConnectionContext _context;

        public SqliteShortcutService(ISqliteConnectionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<ShortcutGroupModel>> GetShortcutGroupsAsync()
        {
            await _context.InitializeAsync();

            var rows = await _context.Db.QueryAsync<ShortcutGroupRow>(
                @"SELECT ShortcutGroupId, Name, Color, ShortcutGroupOrder
                  FROM ShortcutGroup
                  ORDER BY ShortcutGroupOrder ASC, ShortcutGroupId ASC;");

            return rows.Select(ShortcutGroupMapper.ToDomain).ToList();
        }

        /// <summary>
        /// Returns shortcuts ordered by group and shortcut order with each shortcut's group populated.
        /// </summary>
        public async Task<List<ShortcutModel>> GetDashboardShortcutsAsync()
        {
            await _context.InitializeAsync();

            var rows = await _context.Db.QueryAsync<DashboardShortcutJoinRow>(
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
                  ORDER BY g.ShortcutGroupOrder ASC, s.ShortcutOrder ASC, s.ShortcutId ASC;");

            return rows.Select(ShortcutMapper.ToDomain).ToList();
        }

        public async Task<ShortcutGroupModel> UpsertShortcutGroupAsync(ShortcutGroupModel group)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (string.IsNullOrWhiteSpace(group.Name))
                throw new ArgumentException("Group.Name is required.", nameof(group));

            var name = group.Name.Trim();
            var colorHex = NormalizeArgbHex(ToHexArgb(group.Color));
            var order = group.ShortcutGroupOrder;

            ShortcutGroupModel? result = null;

            await _context.RunInTransactionAsync(conn =>
            {
                var existing = conn.Query<ShortcutGroupRow>(
                    @"SELECT ShortcutGroupId, Name, Color, ShortcutGroupOrder
                      FROM ShortcutGroup
                      WHERE Name = ? COLLATE NOCASE
                      LIMIT 1;",
                    name).FirstOrDefault();

                if (existing != null)
                {
                    conn.Execute(
                        @"UPDATE ShortcutGroup
                          SET Color = ?, ShortcutGroupOrder = ?
                          WHERE ShortcutGroupId = ?;",
                        colorHex, order, existing.ShortcutGroupId);

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
                    name, colorHex, order);

                var newId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");

                result = new ShortcutGroupModel
                {
                    ShortcutGroupId = newId,
                    Name = name,
                    Color = ParseColor(colorHex),
                    ShortcutGroupOrder = order
                };
            });

            return result ?? throw new InvalidOperationException("UpsertShortcutGroupAsync failed unexpectedly.");
        }

        public async Task<ShortcutModel> SaveShortcutAsync(ShortcutModel shortcut)
        {
            if (shortcut == null)
                throw new ArgumentNullException(nameof(shortcut));

            if (shortcut.TargetCardId <= 0)
                throw new ArgumentException("TargetCardId must be set.", nameof(shortcut));

            if (shortcut.ShortcutGroupId <= 0)
                throw new ArgumentException("ShortcutGroupId must be set.", nameof(shortcut));

            shortcut.IconChar = (shortcut.IconChar ?? "").Trim();

            var row = ShortcutMapper.ToRow(shortcut);
            var savedId = row.ShortcutId;

            await _context.RunInTransactionAsync(conn =>
            {
                if (savedId <= 0)
                {
                    conn.Execute(
                        @"INSERT INTO Shortcut (IconChar, TargetCardId, ShortcutGroupId, ShortcutOrder)
                          VALUES (?, ?, ?, ?);",
                        row.IconChar, row.TargetCardId, row.ShortcutGroupId, row.ShortcutOrder);

                    savedId = conn.ExecuteScalar<long>("SELECT last_insert_rowid();");
                    return;
                }

                conn.Execute(
                    @"UPDATE Shortcut
                      SET IconChar = ?, TargetCardId = ?, ShortcutGroupId = ?, ShortcutOrder = ?
                      WHERE ShortcutId = ?;",
                    row.IconChar, row.TargetCardId, row.ShortcutGroupId, row.ShortcutOrder, savedId);
            });

            shortcut.ShortcutId = savedId;
            return shortcut;
        }

        public async Task DeleteShortcutAsync(long shortcutId)
        {
            if (shortcutId <= 0)
                return;

            await _context.InitializeAsync();
            await _context.Db.ExecuteAsync("DELETE FROM Shortcut WHERE ShortcutId = ?;", shortcutId);
        }

        public async Task DeleteShortcutGroupAsync(long shortcutGroupId)
        {
            if (shortcutGroupId <= 0)
                return;

            await _context.InitializeAsync();
            await _context.Db.ExecuteAsync("DELETE FROM ShortcutGroup WHERE ShortcutGroupId = ?;", shortcutGroupId);
        }

        private static Color ParseColor(string? hex)
        {
            return Color.FromArgb(NormalizeArgbHex(hex));
        }

        private static string ToHexArgb(Color color)
        {
            var a = (byte)Math.Round(color.Alpha * 255);
            var r = (byte)Math.Round(color.Red * 255);
            var g = (byte)Math.Round(color.Green * 255);
            var b = (byte)Math.Round(color.Blue * 255);
            return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        }

        private static string NormalizeArgbHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return "#FF000000";

            hex = hex.Trim();
            if (!hex.StartsWith("#", StringComparison.Ordinal))
                hex = "#" + hex;

            if (hex.Length == 7)
                return "#FF" + hex[1..];

            return hex.Length == 9 ? hex : "#FF000000";
        }

        private sealed class ShortcutGroupRow
        {
            public long ShortcutGroupId { get; set; }
            public string Name { get; set; } = "";
            public string Color { get; set; } = "#FF000000";
            public int ShortcutGroupOrder { get; set; }
        }

        private sealed class ShortcutRow
        {
            public long ShortcutId { get; set; }
            public string IconChar { get; set; } = "";
            public long TargetCardId { get; set; }
            public long ShortcutGroupId { get; set; }
            public int ShortcutOrder { get; set; }
        }

        private sealed class DashboardShortcutJoinRow
        {
            public long ShortcutId { get; set; }
            public string IconChar { get; set; } = "";
            public long TargetCardId { get; set; }
            public int ShortcutOrder { get; set; }
            public long ShortcutGroupId { get; set; }
            public string GroupName { get; set; } = "";
            public string GroupColor { get; set; } = "#FF000000";
            public int ShortcutGroupOrder { get; set; }
        }

        private static class ShortcutGroupMapper
        {
            public static ShortcutGroupModel ToDomain(ShortcutGroupRow row)
            {
                return new ShortcutGroupModel
                {
                    ShortcutGroupId = row.ShortcutGroupId,
                    Name = row.Name,
                    Color = ParseColor(row.Color),
                    ShortcutGroupOrder = row.ShortcutGroupOrder
                };
            }
        }

        private static class ShortcutMapper
        {
            public static ShortcutRow ToRow(ShortcutModel model)
            {
                return new ShortcutRow
                {
                    ShortcutId = model.ShortcutId,
                    IconChar = model.IconChar ?? "",
                    TargetCardId = model.TargetCardId,
                    ShortcutGroupId = model.ShortcutGroupId,
                    ShortcutOrder = model.ShortcutOrder
                };
            }

            public static ShortcutModel ToDomain(DashboardShortcutJoinRow row)
            {
                return new ShortcutModel
                {
                    ShortcutId = row.ShortcutId,
                    IconChar = row.IconChar,
                    TargetCardId = row.TargetCardId,
                    ShortcutGroupId = row.ShortcutGroupId,
                    ShortcutOrder = row.ShortcutOrder,
                    Group = new ShortcutGroupModel
                    {
                        ShortcutGroupId = row.ShortcutGroupId,
                        Name = row.GroupName,
                        Color = ParseColor(row.GroupColor),
                        ShortcutGroupOrder = row.ShortcutGroupOrder
                    }
                };
            }
        }
    }
}
