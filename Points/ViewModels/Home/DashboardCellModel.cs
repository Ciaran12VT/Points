using Points.Models;

namespace Points.ViewModels.Home
{
    public sealed class DashboardCellModel : ObservableObject
    {
        public bool IsPlaceholder { get; set; }
        public ShortcutModel? Shortcut { get; set; }
        public string IconChar => Shortcut?.IconChar ?? "";
        public Color BackColor => Shortcut?.Group?.Color ?? Colors.Black;
    }
}
