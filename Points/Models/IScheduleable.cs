using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public interface IScheduleable
    {
        public ObservableCollection<CardSchedule> Schedules { get; set; }

        public void SetSchedules(List<CardSchedule> schedules);
    }
}
