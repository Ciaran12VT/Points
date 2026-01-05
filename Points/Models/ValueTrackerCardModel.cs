using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Points.Models;

public partial class ValueTrackerCardModel : TrackerCardModel
{
    // Schedule recording (not executed yet)
    public int ScheduleEvery { get; set; } = 1;
    public string ScheduleUnit { get; set; } = "Week";

    public void AddValue(double value)
    {
        Values.Add(new TrackerValueModel() { Timestamp = DateTime.Now, Value = value });
    }

    public void SetValues(List<double> values)
    {
        Values.Clear();
        foreach (double value in values)
        {
            AddValue(value);
        }
    }
}
