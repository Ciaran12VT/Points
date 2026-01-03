using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Points.Models;

public partial class TrackerCardModel : ObservableObject, ICardModel
{
    // ---- Data ----

    /// <summary>
    /// Ordered list of values (oldest → newest).
    /// This is what the card binds to for the sparkline.
    /// </summary>
    public ObservableCollection<double> Values { get; } = new();

    public ICommand AddValueCommand { get; }

    public string Unit { get; set; } = "";


    // Schedule recording (not executed yet)
    public int ScheduleEvery { get; set; } = 1;
    public string ScheduleUnit { get; set; } = "Week";

    public int Id { get; set; }

    public string Title { get; set; }

    public string Tags { get; set; }

    public DateTime? FirstRecordedDate { get; set; }

    public void AddValue(double value)
    {
        Values.Add(value);
        // No further action needed: TrackerCardView listens to collection changes
    }

    public void SetValues(List<double> values)
    {
        Values.Clear();
        foreach (double value in values)
        {
            Values.Add(value);
        }
        // No further action needed: TrackerCardView listens to collection changes
    }

    public double GetValue(DateTime start, DateTime end)
    {
        return 0;
    }
}
