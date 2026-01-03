using CommunityToolkit.Mvvm.ComponentModel;
using Points.Models;
using Points.Views.Cards;

namespace Points.ViewModels;

public partial class TrackerCardViewModel : Models.ObservableObject
{
    private TrackerCardModel _model;

    //[ObservableProperty] private string title = "";

    // Values for the sparkline. Keep them ordered by time.
    // In a real app you’d probably expose the raw entries and project to values.
    public IList<double> Values { get; set; } = new List<double>();

    // The GraphicsView binds to this.
    public SparklineDrawable SparklineDrawable { get; } = new();

    public string LastValueText =>
        Values.Count == 0 ? "No entries yet" : $"Last: {Values[^1]:0.###}";

    public string DeltaText
    {
        get
        {
            if (Values.Count < 2) return "";
            var delta = Values[^1] - Values[^2];
            var sign = delta > 0 ? "+" : "";
            return $"{sign}{delta:0.###}";
        }
    }

    public TrackerCardViewModel(TrackerCardModel model)
    {
        _model = model;
    }

    public void RefreshSparkline()
    {
        SparklineDrawable.Values = Values;

        // Important: GraphicsView won’t redraw unless it’s invalidated.
        // Easiest is to expose an event the view listens to, or
        // reassign SparklineDrawable (heavy).
        //
        // For v1: you can keep a reference to the GraphicsView and call Invalidate().
        // I’ll show the clean pattern next.
        RaisePropertyChanged(nameof(LastValueText));
        RaisePropertyChanged(nameof(DeltaText));
    }
}
