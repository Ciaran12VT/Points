using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.Models
{
    public abstract class TrackerCardModel : ObservableObject, ICardModel
    {

        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Tags { get; set; } = "";


        public ObservableCollection<TrackerValueModel> Values { get; } = new();

        public ICommand? AddValueCommand { get; protected set; }

        public string Unit { get; set; } = "";

        public DateTime CreatedDate { get; set; }

        public DateTime RangeStart { get; set; }

        public virtual void AddValue(TrackerValueModel value) => Values.Add(value);

        public virtual void SetValues(List<TrackerValueModel> values)
        {
            Values.Clear();
            foreach (TrackerValueModel value in values)
            {
                Values.Add(value);
            }
        }

        public double GetValue(DateTime start, DateTime end) => 0;

    }

    public class TrackerValueModel
    {
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        public double Value { get; set; }
    }
}
