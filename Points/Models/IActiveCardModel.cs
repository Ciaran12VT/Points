using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.Models
{
    public interface IActiveCardModel : ICardModel
    {
        bool IsActive { get; }
        ICommand ToggleActivityCommand { get; }

        void StopActivity();

        // Keep your requested shape for now
        List<Tuple<DateTime, DateTime>> Activity { get; }
        double ValuePerMinute { get; }

        TimeSpan GetActiveTime(DateTime start, DateTime end);
    }
}
