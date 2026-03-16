using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public sealed class ToggleActivityModelResult
    {
        public ActivityModel? Closed { get; init; }
        public ActivityModel? Opened { get; init; }
    }
}
