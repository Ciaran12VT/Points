using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public sealed class ReportModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string SQLQuery { get; set; } = "";
        public DateTime? LastRunOn { get; set; }
        public bool EligibleForAchievment { get; set; }
    }
}
