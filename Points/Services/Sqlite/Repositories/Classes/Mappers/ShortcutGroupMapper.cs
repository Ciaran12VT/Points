using Points.Models;

namespace Points.Services.Sqlite
{
    public sealed partial class ShortcutRepository
    {
        #region Row Models

        #endregion

        #region Mappers

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

            public static ShortcutGroupRow ToRow(ShortcutGroupModel model)
            {
                return new ShortcutGroupRow
                {
                    ShortcutGroupId = model.ShortcutGroupId,
                    Name = model.Name ?? "",
                    Color = NormalizeArgbHex(ToHexArgb(model.Color)),
                    ShortcutGroupOrder = model.ShortcutGroupOrder
                };
            }
        }

        #endregion
    }
}