namespace Points.Services.Sqlite
{
    public sealed partial class ShortcutRepository
    {
        #region Row Models

        private sealed class ShortcutRow
        {
            public long ShortcutId { get; set; }
            public string IconChar { get; set; } = "";
            public long TargetCardId { get; set; }
            public long ShortcutGroupId { get; set; }
            public int ShortcutOrder { get; set; }
        }

        #endregion
    }
}