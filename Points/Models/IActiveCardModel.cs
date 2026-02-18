using Points.Evaluators;
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
        long CardID { get; set; }
        bool IsActive { get; set; }
        ICommand ToggleActivityCommand { get; }

        void StopActivity();
        void Activitate();

        // Keep your requested shape for now
        List<ActivityModel> Activity { get; }
        double ValuePerMinute { get; }

        TimeSpan GetActiveTime(DateTime start, DateTime end);

        DateTime GetLastActiveTime();

        List<TimeValueAchievementEvaluator> TimeValueAchievementEvaluators { get; set; }
    }
}
