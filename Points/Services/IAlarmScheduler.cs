using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services
{
    public interface IAlarmScheduler
    {
        Task ScheduleAllAsync(IEnumerable<CardSchedule> schedules, CancellationToken ct = default);
        Task ScheduleOneAsync(CardSchedule schedule, CancellationToken ct = default);
        Task CancelOneAsync(long scheduleId);
        Task CancelAllAsync(IEnumerable<long> scheduleIds);
    }

}
