using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    // Points.Models

    public sealed class ShortcutGroupModel
    {
        public long ShortcutGroupId { get; set; }
        public string Name { get; set; } = "";
        public Color Color { get; set; } = Colors.Black;
        public int ShortcutGroupOrder { get; set; }
    }

    public sealed class ShortcutModel
    {
        public long ShortcutId { get; set; }
        public string IconChar { get; set; } = "";

        public long TargetCardId { get; set; }

        public long ShortcutGroupId { get; set; }
        public int ShortcutOrder { get; set; }

        // Convenient for Dashboard rendering (JOIN result)
        public ShortcutGroupModel? Group { get; set; }
    }
}
