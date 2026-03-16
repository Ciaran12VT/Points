using __XamlGeneratedCode__;
using CommunityToolkit.Mvvm.ComponentModel;
using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Views.Cards;
using System.Collections.Specialized;
using System.Windows.Input;

namespace Points.ViewModels;

public partial class TrackerCardViewModel : Models.ObservableObject
{
    public TrackerCardModel Model { get; }

    // The GraphicsView binds to this.
    public SparklineDrawable SparklineDrawable { get; } = new();

    public ICommand AddValueCommand { get; }

    private readonly IDbService _db;

    public TrackerCardViewModel(TrackerCardModel model, IDbService db)
    {
        Model = model;

        _db = db;

        // Keep sparkline updated when Values changes.
        Model.Values.CollectionChanged += OnValuesChanged;

        // Initial draw
        RefreshSparkline();

        AddValueCommand = new Command<object>(async (param) =>
        {
            // Prefer the passed model, else fall back to Model
            var card = param as TrackerCardModel ?? Model;
            await AddValueAsync(card);
        });
    }

    private void OnValuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshSparkline();
    }

    // Project to doubles for the drawable (ordered by time)
    private IList<double> ProjectedValues =>
        Model.Values
            .OrderBy(v => v.Timestamp)
            .Select(v => v.Value)
            .ToList();

    public string LastValueText =>
        Model.Values.Count == 0 ? "No entries yet" : $"Last: {ProjectedValues[^1]:0.###}";

    public string DeltaText
    {
        get
        {
            var vals = ProjectedValues;
            if (vals.Count < 2) return "";
            var delta = vals[^1] - vals[^2];
            var sign = delta > 0 ? "+" : "";
            return $"{sign}{delta:0.###}";
        }
    }

    public void RefreshSparkline()
    {
        SparklineDrawable.Values = ProjectedValues;

        RaisePropertyChanged(nameof(LastValueText));
        RaisePropertyChanged(nameof(DeltaText));

        // If your view currently needs an explicit invalidate call,
        // we’ll handle that in TrackerCardView (next step) OR expose an event here.
    }

    private async Task AddValueAsync(TrackerCardModel card)
    {
        switch (card)
        {
            case ValueTrackerCardModel valueTracker:
                await AddValueToValueTrackerAsync(valueTracker);
                break;

            case EventTrackerCardModel eventTracker:
                eventTracker.AddValue();
                break;
        }

        await _db.SaveCardModelAsync(card);
    }

    private async Task AddValueToValueTrackerAsync(ValueTrackerCardModel valueTracker)
    {
        var page = Shell.Current?.CurrentPage;
        if (page == null)
            return;

        var input = await page.DisplayPromptAsync(
            "Add Value",
            "Enter a value:",
            accept: "OK",
            cancel: "Cancel",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (!double.TryParse(input, out var value))
        {
            await page.DisplayAlert("Invalid value", "Please enter a valid number.", "OK");
            return;
        }

        valueTracker.AddValue(value);
    }
}

public interface IDialogService
{
    Task<string?> PromptAsync(string title, string message);
    Task AlertAsync(string title, string message);
}

