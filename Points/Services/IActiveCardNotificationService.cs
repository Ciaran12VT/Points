using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services
{
    public interface IActiveCardNotificationService
    {
        /// <summary>
        /// Update the foreground notification with the active card title.
        /// Pass null to clear/stop the notification.
        /// </summary>
        void UpdateActiveCardNotification(IActiveCardModel? cardModel);

        void DebugBeep();
    }
}
