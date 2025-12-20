using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class ScStepModel : ObservableObject
    {
        private int _order;
        public int Order
        {
            get => _order;
            set => SetProperty(ref _order, value);
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

        private int _count;
        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        public Command IncrementCommand { get; }
        public Command DecrementCommand { get; }

        public ScStepModel()
        {
            IncrementCommand = new Command(() => Count++);
            DecrementCommand = new Command(() => { if (Count > 0) Count--; });
        }
    }
}
