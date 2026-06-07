using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Points.Services.Activity;

namespace Points.Models
{
    public class ScCardModel : TatCardModel
    {
        public bool IsSingleStep => Steps.Count == 1;

        public double FirstStepRepCount => Steps.Count > 0 ? Steps[0].Reps.Count : 0;

        public ScCardModel()
        {
            Steps.CollectionChanged += Steps_CollectionChanged;
        }

        private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (ScStepModel step in e.OldItems)
                    step.PropertyChanged -= Step_PropertyChanged;

            if (e.NewItems != null)
                foreach (ScStepModel step in e.NewItems)
                    step.PropertyChanged += Step_PropertyChanged;

            // Steps.Count may have changed
            RaisePropertyChanged(nameof(IsSingleStep));

            // The “first step” may have changed (insert/remove/move), so refresh rep count too
            RaisePropertyChanged(nameof(FirstStepRepCount));
        }

        private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Only rep changes need to refresh the computed rep-count
            if (e.PropertyName == nameof(ScStepModel.RepsVersion))
            {
                // If you want this to be strictly “first step only”, you can check sender == Steps[0]
                RaisePropertyChanged(nameof(FirstStepRepCount));
            }
        }

        public ObservableCollection<ScStepModel> Steps { get; set; } = new();

        // SC value is: sum( StepValue * Count ) with sign controlled by ValuePerMinute sign
        // We’ll use ValuePerMinute’s sign as the “Positive/Negative toggle”, but treat its magnitude as irrelevant.
        public override double GetValue(DateTime start, DateTime end)
        {
            var sum = Steps.Sum(s => s.StepValue * s.Count(start, end));
            var sign = ValuePerMinute < 0 ? -1 : 1;
            return sign * sum;
        }

        public override DateTime GetLastActiveTime()
        {
            var defaulValue = base.GetLastActiveTime();
            return CardRecencyCalculator.Latest(defaulValue, Steps.SelectMany(x => x.Reps));
        }
    }
}
