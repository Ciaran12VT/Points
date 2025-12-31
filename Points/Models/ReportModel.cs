using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public sealed class ReportModel
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Title { get; init; } = "";
        public string SQLQuery { get; set; } = "";
        public DateTime? LastRunOn { get; set; }
        public bool EligibleForAchievment { get; set; }
    }
}
