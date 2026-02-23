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
        List<ActivityModel> Activity { get; }
        double ValuePerMinute { get; }

        TimeSpan GetActiveTime(DateTime start, DateTime end);

        DateTime GetLastActiveTime();

        List<TimeValueAchievementEvaluator> TimeValueAchievementEvaluators { get; set; }

        List<LockModel> Locks { get; set; }
    }
}
