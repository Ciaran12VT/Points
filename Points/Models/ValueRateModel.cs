using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class ValueRateModel : ObservableObject
    {
        public string RateName { get; set; }
        public double ValuePerMinute { get; set; }
    }
}
