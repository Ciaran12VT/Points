using Points.Models;

namespace Points.Services.Sqlite
{
    public sealed partial class ShortcutRepository
    {
        #region Mappers

        private static class ShortcutMapper
        {
            public static ShortcutModel ToDomain(ShortcutRow row)
            {
                return new ShortcutModel
                {
                    ShortcutId = row.ShortcutId,
                    IconChar = row.IconChar,
                    TargetCardId = row.TargetCardId,
                    ShortcutGroupId = row.ShortcutGroupId,
                    ShortcutOrder = row.ShortcutOrder,
                    Group = null
                };
            }

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

        #endregion
    }
}