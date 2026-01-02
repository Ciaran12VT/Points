using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class ScStepModel : ObservableObject
    {
        public int Id { get; set; }

        private int _sortOrder;
        public int SortOrder
        {
            get => _sortOrder;
            set => SetProperty(ref _sortOrder, value);
        }

        private string _title = "";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private double _stepValue = 1.0;
        public double StepValue
        {
            get => _stepValue;
            set => SetProperty(ref _stepValue, value);
        }

        public List<DateTime> Reps = new List<DateTime>();
        public int Count(DateTime start, DateTime end)
        {
            return Reps.Count(x => x >= start && x <= end);
        }

        // This exists purely to trigger UI updates for converters.
        private int _repsVersion;
        public int RepsVersion
        {
            get => _repsVersion;
            private set => SetProperty(ref _repsVersion, value);
        }

        public Command IncrementCommand { get; }
        public Command DecrementCommand { get; }

        public ScStepModel()
        {
            IncrementCommand = new Command(() =>
            {
                Reps.Add(DateTime.Now);
                RepsVersion++;
            });

            DecrementCommand = new Command(() =>
            {
                if (Reps.Count > 0)
                {
                    Reps.RemoveAt(Reps.Count - 1);
                    RepsVersion++;
                }
            });
        }
    }
}
