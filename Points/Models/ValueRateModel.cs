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
        public string RateName { get; set; }
        public double ValuePerMinute { get; set; }

        public ICommand DeleteValueRateCommand { get; }

        private Action<ValueRateModel> _delete;

        public ValueRateModel(Action<ValueRateModel> delete)
        {
            _delete = delete;
            DeleteValueRateCommand = new Command(() => _delete(this));
        }

        public ValueRateModel()
        {
                
        }
    }
}
