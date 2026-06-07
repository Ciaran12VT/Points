using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.Models
{
    public class ValueRateModel : ObservableObject
    {
        public int Id { get; set; }
        public string RateName { get; set; } = "";
        public double ValuePerMinute { get; set; }

        public ICommand DeleteValueRateCommand { get; }

        private Action<ValueRateModel> _delete = _ => { };

        public ValueRateModel(Action<ValueRateModel> delete)
            : this()
        {
            _delete = delete;
        }

        public ValueRateModel()
        {
            DeleteValueRateCommand = new Command(() => _delete(this));
        }
    }
}
