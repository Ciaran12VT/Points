using Points.Models;
using Points.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services
{
    public class NullActiveCardNotificationService : IActiveCardNotificationService
    {
        public void UpdateActiveCardNotification(IActiveCardModel? activeCardModel)
        {
            // intentionally does nothing
        }
    }
}
