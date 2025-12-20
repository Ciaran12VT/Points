using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class ScheduledTopUp
    {
        // Time of day the top-up happens (local time)
        public TimeSpan TimeOfDay { get; set; }

        // Amount in budget currency
        public double Amount { get; set; }
    }
}
