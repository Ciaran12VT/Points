using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Global
{
    public static class GlobalVariables
    {
        public static DateTime RangeStart { get; set; } = DateTime.Today;

        public static DateTime RangeEnd { get; set;} = DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(59);
    }
}
