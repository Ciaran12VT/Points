using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.Models
{
    public interface IActiveCardModel : ICardModel
    {
        public int Id { get; set; }
        bool IsActive { get; }
        ICommand ToggleActivityCommand { get; }

        void StopActivity();

        // Keep your requested shape for now
        List<ActivityModel> Activity { get; }
        double ValuePerMinute { get; }

        TimeSpan GetActiveTime(DateTime start, DateTime end);

        DateTime GetLastActiveTime();
    }

    public class ActivityModel
    {
        public ActivityModel(DateTime start, DateTime end, string rate, double value)
        {
            StartDate = start;
            EndDate = end;
            RateName = rate;
            ValuePerMinute = value;
        }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string RateName { get; set; }
        public double ValuePerMinute { get; set; }
    }
}
