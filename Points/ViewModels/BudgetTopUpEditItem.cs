using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class BudgetTopUpEditItem : ObservableObject
    {
        private string _amountText = "500";
        public string AmountText
        {
            get => _amountText;
            set => SetProperty(ref _amountText, value);
        }

        private TimeSpan _timeOfDay = new(7, 0, 0);
        public TimeSpan TimeOfDay
        {
            get => _timeOfDay;
            set => SetProperty(ref _timeOfDay, value);
        }
    }
}
