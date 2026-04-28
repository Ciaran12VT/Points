using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class EventTrackerCardModel : TrackerCardModel
    {
        public string GroupByPeriod { get; set; } = "Day";

        public void AddValue()
        {
            Values.Add(new TrackerValueModel() { Timestamp = ActivityTimeMath.UtcNow, Value = 1 });
        }

        public void SetValues(List<DateTime> values)
        {
            Values.Clear();
            foreach (DateTime value in values)
            {
                Values.Add(new TrackerValueModel() { Timestamp = value, Value = 1 });
            }
        }
    }
}
