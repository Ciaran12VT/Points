namespace Points.Services.Sqlite
{
    public sealed partial class ShortcutRepository
    {
        #region Row Models

        private sealed class ShortcutGroupRow
        {
            public long ShortcutGroupId { get; set; }
            public string Name { get; set; } = "";
            public string Color { get; set; } = "#FF000000";
            public int ShortcutGroupOrder { get; set; }
        }

        #endregion
    }
}