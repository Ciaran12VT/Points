using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    internal class DateHeaderCardModel : ICardModel
    {
        public int Id { get; set; }
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }
        public string Title { get; set; } = "";

        public string Tags { get; set; } = "";

        public double GetValue(DateTime start, DateTime end)
        {
            return 0;
        }
    }
}
