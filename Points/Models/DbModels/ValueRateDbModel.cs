using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models.DbModels
{
    public class ValueRateDbModel
    {
        public int TatCardValueRateID { get; set; }
        public int TatCardID { get; set; }
        public string RateName { get; set; } = "";
        public double ValuePerMinute { get; set; }
    }
}
