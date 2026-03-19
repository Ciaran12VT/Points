namespace Points.Services.Sqlite
{
    public sealed partial class ShortcutRepository
    {
        #region Row Models

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

        #endregion
    }
}