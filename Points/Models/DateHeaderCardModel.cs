using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    internal class DateHeaderCardModel : ICardModel
    {
        public string Id { get; } = Guid.NewGuid().ToString();

        public string Title { get; set; } = "";

        public string Tags { get; set; } = "";

        public double GetValue(DateTime start, DateTime end)
        {
            return 0;
        }
    }
}
