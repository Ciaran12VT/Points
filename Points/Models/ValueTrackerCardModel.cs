using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Points.Models;

public partial class ValueTrackerCardModel : TrackerCardModel, IScheduleable
{
    // Legacy schedule recording (to be removed once CardSchedule persistence is active)
    public int ScheduleEvery { get; set; } = 1;
    public string ScheduleUnit { get; set; } = "Week";

    public ObservableCollection<CardSchedule> Schedules { get; set; } = new();


    public void AddValue(double value)
    {
        Values.Add(new TrackerValueModel() { Timestamp = ActivityTimeMath.UtcNow, Value = value });
    }

    public void SetValues(List<double> values)
    {
        Values.Clear();
        foreach (double value in values)
        {
            AddValue(value);
        }
    }

    public void SetSchedules(List<CardSchedule> schedules)
    {
        Schedules.Clear();
        foreach (var s in schedules)
            Schedules.Add(s);
    }

}
