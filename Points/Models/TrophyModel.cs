using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Points.Services.Time;

namespace Points.Models
{
    public class TrophyModel
    {
        public int Id { get; set; }

        public int AchievementId { get; set; }

        public string Title { get; set; } = "";
        public DateTime EarnedOn { get; set; }

        // For now, use an embedded/app image or a file name in Resources/Images
        // e.g. "trophy.png" (add it to Resources/Images)
        public string ImageSource { get; set; } = "trophy.png";

        public string EarnedOnText => TimeDisplayFormatter.FormatInstant(EarnedOn, "yyyy-MM-dd");
    }
}

