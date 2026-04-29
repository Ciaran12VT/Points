using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public sealed class CardSchedule : IScheduleModel
    {
        public long ScheduleId { get; set; }     // PK
        public long CardId { get; set; }         // ties schedule to any card type

        public FrequencyType FrequencyType { get; set; }
        public int FrequencyValue { get; set; }

        public DateTime FromDateTime { get; set; }
        public DateTime? ToDateTime { get; set; }

        public bool IsEnabled { get; set; } = true;
        public string? Note { get; set; } = "";

        public CardSchedule Clone() =>
            new()
            {
                ScheduleId = this.ScheduleId,
                CardId = this.CardId,
                FrequencyType = this.FrequencyType,
                FrequencyValue = this.FrequencyValue,
                FromDateTime = this.FromDateTime,
                ToDateTime = this.ToDateTime,
                IsEnabled = this.IsEnabled,
                Note = this.Note
            };
    }

    public enum FrequencyType
    {
        Once, //Ignores the FrequencyValue and ScheduleRange.ToDateTime and just triggers the event on the ScheduleRange.FromDateTime
        EveryDays, //Between ScheduleRange.FromDateTime and ScheduleRange.ToDateTime, trigger event every FrequencyValue number of days
        EveryMonday, EveryTuesday, EveryWednesday, EveryThursday, EveryFriday, EverySaturday, EverySunday, //Between ScheduleRange.FromDateTime and ScheduleRange.ToDateTime, trigger event every relevant weekday. Ignore FrequencyValue.
        EveryWeekday, //Between ScheduleRange.FromDateTime and ScheduleRange.ToDateTime, trigger event every single weekday. Ignore FrequencyValue.
        EveryWeeks, //Between ScheduleRange.FromDateTime and ScheduleRange.ToDateTime, trigger event once a week on the ScheduleRange.FromDateTime day and time. Ignore FrequencyValue.
        EveryMonths, //Between ScheduleRange.FromDateTime and ScheduleRange.ToDateTime, trigger event once a month on the ScheduleRange.FromDateTime day and time. Ignore FrequencyValue.
        EveryYears //Between ScheduleRange.FromDateTime and ScheduleRange.ToDateTime, trigger event once a year on the ScheduleRange.FromDateTime day and time. Ignore FrequencyValue.
    }

}
