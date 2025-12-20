using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class ScCardModel : TatCardModel
    {

        public ObservableCollection<ScStepModel> Steps { get; } = new();

        // SC value is: sum( StepValue * Count ) with sign controlled by ValuePerMinute sign
        // We’ll use ValuePerMinute’s sign as the “Positive/Negative toggle”, but treat its magnitude as irrelevant.
        public override double GetValue(DateTime start, DateTime end)
        {
            var sum = Steps.Sum(s => s.StepValue * s.Count);
            var sign = ValuePerMinute < 0 ? -1 : 1;
            return sign * sum;
        }
    }
}
