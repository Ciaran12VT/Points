using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public interface IScheduleModel
    {
        FrequencyType FrequencyType { get; set; }
        int FrequencyValue { get; set; }

        DateTime FromDateTime { get; set; }
        DateTime? ToDateTime { get; set; }

        bool IsEnabled { get; set; }
        string? Note { get; set; }
    }
}
