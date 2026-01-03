using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Points.Views.Cards;

public partial class TrackerCardView : ContentView
{
    private readonly SparklineDrawable _drawable = new();

    public TrackerCardView()
    {
        InitializeComponent();

        SparklineView.Drawable = _drawable;
        SyncDrawableAndInvalidate();
    }

    // -----------------------------
    // Bindable Properties (inputs)
    // -----------------------------

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TrackerCardView), defaultValue: "");

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly BindableProperty UnitProperty =
        BindableProperty.Create(
            nameof(Unit),
            typeof(string),
            typeof(TrackerCardView),
            defaultValue: "",
            propertyChanged: (b, _, __) => ((TrackerCardView)b).RecomputeText());

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly BindableProperty ValuesProperty =
        BindableProperty.Create(
            nameof(Values),
            typeof(IList<double>),
            typeof(TrackerCardView),
            defaultValue: null,
            propertyChanged: OnValuesChanged);

    public IList<double>? Values
    {
        get => (IList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly BindableProperty FirstRecordedDateProperty =
        BindableProperty.Create(
            nameof(FirstRecordedDate),
            typeof(DateTime?),
            typeof(TrackerCardView),
            defaultValue: null,
            propertyChanged: (b, _, __) => ((TrackerCardView)b).RecomputeText());

    public DateTime? FirstRecordedDate
    {
        get => (DateTime?)GetValue(FirstRecordedDateProperty);
        set => SetValue(FirstRecordedDateProperty, value);
    }

    // -----------------------------
    // Computed outputs (read-only)
    // -----------------------------

    private static readonly BindablePropertyKey AverageTextPropertyKey =
        BindableProperty.CreateReadOnly(nameof(AverageText), typeof(string), typeof(TrackerCardView), defaultValue: "Avg: —");

    public static readonly BindableProperty AverageTextProperty = AverageTextPropertyKey.BindableProperty;

    public string AverageText
    {
        get => (string)GetValue(AverageTextProperty);
        private set => SetValue(AverageTextPropertyKey, value);
    }

    private static readonly BindablePropertyKey PeriodTextPropertyKey =
        BindableProperty.CreateReadOnly(nameof(PeriodText), typeof(string), typeof(TrackerCardView), defaultValue: "From: —");

    public static readonly BindableProperty PeriodTextProperty = PeriodTextPropertyKey.BindableProperty;

    public string PeriodText
    {
        get => (string)GetValue(PeriodTextProperty);
        private set => SetValue(PeriodTextPropertyKey, value);
    }

    private static readonly BindablePropertyKey TrendArrowPropertyKey =
        BindableProperty.CreateReadOnly(nameof(TrendArrow), typeof(string), typeof(TrackerCardView), defaultValue: "→");

    public static readonly BindableProperty TrendArrowProperty = TrendArrowPropertyKey.BindableProperty;

    public string TrendArrow
    {
        get => (string)GetValue(TrendArrowProperty);
        private set => SetValue(TrendArrowPropertyKey, value);
    }

    private static readonly BindablePropertyKey LatestValueTextPropertyKey =
    BindableProperty.CreateReadOnly(
        nameof(LatestValueText),
        typeof(string),
        typeof(TrackerCardView),
        defaultValue: "—");

    public static readonly BindableProperty LatestValueTextProperty =
        LatestValueTextPropertyKey.BindableProperty;

    public string LatestValueText
    {
        get => (string)GetValue(LatestValueTextProperty);
        private set => SetValue(LatestValueTextPropertyKey, value);
    }



    public static readonly BindableProperty AddValueCommandProperty =
    BindableProperty.Create(
        nameof(AddValueCommand),
        typeof(ICommand),
        typeof(TrackerCardView),
        defaultValue: null);

    public ICommand? AddValueCommand
    {
        get => (ICommand?)GetValue(AddValueCommandProperty);
        set => SetValue(AddValueCommandProperty, value);
    }

    private async void OnAddValueClicked(object sender, EventArgs e)
    {
        if (BindingContext is not Points.Models.TrackerCardModel t)
            return;

        var unit = string.IsNullOrWhiteSpace(t.Unit) ? "" : $" ({t.Unit})";

        var input = await Shell.Current.DisplayPromptAsync(
            "Add Value",
            $"Enter a value for {t.Title}{unit}",
            accept: "OK",
            cancel: "Cancel",
            placeholder: "e.g. 72.4",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input))
            return;

        // Budget card uses InvariantCulture parse; we’ll keep the same pattern for consistency.
        // (If you want comma support later, we can add a fallback parse.)
        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            await Shell.Current.DisplayAlert("Invalid number", "Please enter a valid number.", "OK");
            return;
        }

        // Optional: prevent nonsense values (you can relax this if you want negative trackers)
        // If you want to allow negatives for some trackers, remove this check or add a flag on the tracker.
        // if (value < 0) return;

        t.AddValue(value);

        // If you have DB persistence already (or soon), this is where it should go:
        // await vm.AddTrackerEntryAsync(t.Id, DateTime.Now, value);
    }


    // -----------------------------
    // Change handling
    // -----------------------------

    private INotifyCollectionChanged? _observableValues;

    private static void OnValuesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (TrackerCardView)bindable;

        if (oldValue is INotifyCollectionChanged oldObs)
            oldObs.CollectionChanged -= view.OnValuesCollectionChanged;

        if (newValue is INotifyCollectionChanged newObs)
        {
            view._observableValues = newObs;
            newObs.CollectionChanged += view.OnValuesCollectionChanged;
        }
        else
        {
            view._observableValues = null;
        }

        view.SyncDrawableAndInvalidate();
    }

    private void OnValuesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => SyncDrawableAndInvalidate();

    private void SyncDrawableAndInvalidate()
    {
        _drawable.Values = Values ?? Array.Empty<double>();
        RecomputeText();
        SparklineView?.Invalidate();
    }

    private void RecomputeText()
    {
        var vals = Values;
        var unit = string.IsNullOrWhiteSpace(Unit) ? "" : $" {Unit}";

        if (vals == null || vals.Count == 0)
        {
            AverageText = "Avg: —";
            LatestValueText = "—";
            TrendArrow = "→";
        }
        else
        {
            var last = vals[^1];
            var avg = vals.Average();
            AverageText = $"Avg: {avg:0.###}{unit}";
            LatestValueText = $"{last:0.###}{unit}";
            TrendArrow = ComputeTrendArrow(vals, avg);
        }

        PeriodText = FirstRecordedDate.HasValue
            ? BuildOverTheLastText(FirstRecordedDate.Value, DateTime.Now)
            : "Over the last —";
    }

    private static string BuildOverTheLastText(DateTime start, DateTime now)
    {
        if (now < start)
            return "Over the last 0 minutes";

        var ts = now - start;

        // < 1 hour -> minutes
        if (ts.TotalHours < 1)
        {
            int minutes = Math.Max(0, (int)Math.Floor(ts.TotalMinutes));
            return $"Over the last {minutes} {Plural(minutes, "minute")}";
        }

        // < 1 day -> hours
        if (ts.TotalDays < 1)
        {
            int hours = Math.Max(0, (int)Math.Floor(ts.TotalHours));
            return $"Over the last {hours} {Plural(hours, "hour")}";
        }

        // < 14 days -> days (keeps "days" for short spans, otherwise weeks reads better)
        if (ts.TotalDays < 14)
        {
            int days = Math.Max(0, (int)Math.Floor(ts.TotalDays));
            return $"Over the last {days} {Plural(days, "day")}";
        }

        // For weeks / months / years we need calendar-aware differences,
        // because "months" and "years" are not fixed durations.
        return $"Over the last {BuildCalendarDuration(start, now)}";
    }

    private static string BuildCalendarDuration(DateTime start, DateTime end)
    {
        // Normalize to local kinds if you’re mixing kinds; here we just treat them as-is.
        // We'll compute a (years, months, days) style diff first.
        // Then we’ll choose whether to report as weeks, months, or years.

        // If under ~8 weeks, prefer weeks+days (matches your requirement)
        var totalDays = (int)Math.Floor((end - start).TotalDays);
        if (totalDays < 60) // ~2 months
        {
            int weeks = totalDays / 7;
            int days = totalDays % 7;

            if (weeks <= 0) // fallback
                return $"{totalDays} {Plural(totalDays, "day")}";

            if (days > 0)
                return $"{weeks} {Plural(weeks, "week")} {days} {Plural(days, "day")}";

            return $"{weeks} {Plural(weeks, "week")}";
        }

        // For >= ~2 months, compute real calendar months/years
        var (years, months, daysRemainder) = DiffYearsMonthsDays(start, end);

        // If < 1 year, show months + days (per your requirement)
        if (years == 0)
        {
            // If months is 0 (edge case), fall back to weeks/days
            if (months == 0)
            {
                int weeks = totalDays / 7;
                int days = totalDays % 7;
                return days > 0
                    ? $"{weeks} {Plural(weeks, "week")} {days} {Plural(days, "day")}"
                    : $"{weeks} {Plural(weeks, "week")}";
            }

            if (daysRemainder > 0)
                return $"{months} {Plural(months, "month")} {daysRemainder} {Plural(daysRemainder, "day")}";

            return $"{months} {Plural(months, "month")}";
        }

        // For >= 1 year, show years + months + days (per your requirement)
        var parts = new List<string>();

        parts.Add($"{years} {Plural(years, "year")}");

        if (months > 0)
            parts.Add($"{months} {Plural(months, "month")}");

        if (daysRemainder > 0)
            parts.Add($"{daysRemainder} {Plural(daysRemainder, "day")}");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Calendar-aware diff: returns how many whole years and months fit, then remaining days.
    /// </summary>
    private static (int years, int months, int days) DiffYearsMonthsDays(DateTime start, DateTime end)
    {
        if (end < start) return (0, 0, 0);

        // Work on date component but preserve time-of-day for day remainder correctness
        var anchor = start;

        int years = 0;
        while (anchor.AddYears(1) <= end)
        {
            anchor = anchor.AddYears(1);
            years++;
        }

        int months = 0;
        while (anchor.AddMonths(1) <= end)
        {
            anchor = anchor.AddMonths(1);
            months++;
        }

        int days = (int)Math.Floor((end - anchor).TotalDays);
        if (days < 0) days = 0;

        return (years, months, days);
    }

    private static string Plural(int n, string singular) => n == 1 ? singular : singular + "s";


    /// <summary>
    /// Arrow meanings:
    /// ↑  : latest > prev AND latest >= avg
    /// ↗  : latest > prev BUT latest < avg
    /// →  : latest ~== prev (within epsilon)
    /// ↘  : latest < prev BUT latest >= avg (your example)
    /// ↓  : latest < prev AND latest < avg (your example)
    /// </summary>
    private static string ComputeTrendArrow(IList<double> vals, double avg)
    {
        if (vals.Count < 2) return "→";

        double prev = vals[^2];
        double last = vals[^1];

        const double eps = 1e-9;

        double delta = last - prev;

        if (Math.Abs(delta) <= eps) return "→";

        bool upVsPrev = delta > 0;
        bool aboveAvg = last >= avg - eps;

        if (upVsPrev && aboveAvg) return "↑";
        if (upVsPrev && !aboveAvg) return "↗";
        if (!upVsPrev && aboveAvg) return "↘";
        return "↓";
    }
}
