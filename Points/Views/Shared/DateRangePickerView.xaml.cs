using System.ComponentModel;

namespace Points.Views.Shared;

public partial class DateRangePickerView : ContentView
{
    public static readonly BindableProperty RangeStartProperty =
        BindableProperty.Create(
            nameof(RangeStart),
            typeof(DateTime),
            typeof(DateRangePickerView),
            DateTime.Today,
            BindingMode.TwoWay,
            propertyChanged: (_, __, ___) => { });

    public static readonly BindableProperty RangeEndProperty =
        BindableProperty.Create(
            nameof(RangeEnd),
            typeof(DateTime),
            typeof(DateRangePickerView),
            DateTime.Today.AddDays(1).AddTicks(-1),
            BindingMode.TwoWay,
            propertyChanged: (_, __, ___) => { });

    public DateTime RangeStart
    {
        get => (DateTime)GetValue(RangeStartProperty);
        set => SetValue(RangeStartProperty, value);
    }

    public DateTime RangeEnd
    {
        get => (DateTime)GetValue(RangeEndProperty);
        set => SetValue(RangeEndProperty, value);
    }

    bool _suppress;

    public DateRangePickerView()
    {
        InitializeComponent();

        // Default selection
        RangeModePicker.SelectedIndex = 0; // Today
        ApplyPreset("Today");
    }

    void OnRangeModeChanged(object sender, EventArgs e)
    {
        if (_suppress) return;

        var mode = RangeModePicker.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(mode)) return;

        SingleDateContainer.IsVisible = mode == "Date";

        // Only "Custom" should leave the user's manual selections alone.
        if (mode == "Custom")
            return;

        if (mode == "Date")
        {
            // If the user already picked a date, apply it; otherwise don't override.
            // (DatePicker always has a Date value, but we treat "already picked" as: keep current picker's date.)
            ApplySingleDate(SingleDatePicker.Date);
            return;
        }

        ApplyPreset(mode);
    }

    void OnSingleDateSelected(object sender, DateChangedEventArgs e)
    {
        if (_suppress) return;
        ApplySingleDate(e.NewDate);
    }

    void OnStartChanged(object sender, DateChangedEventArgs e)
    {
        if (_suppress) return;
        RangeStart = Combine(e.NewDate, StartTimePicker.Time);
    }

    void OnStartTimeChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_suppress) return;
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        RangeStart = Combine(StartDatePicker.Date, StartTimePicker.Time);
    }

    void OnEndChanged(object sender, DateChangedEventArgs e)
    {
        if (_suppress) return;
        RangeEnd = Combine(e.NewDate, EndTimePicker.Time);
    }

    void OnEndTimeChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_suppress) return;
        if (e.PropertyName != nameof(TimePicker.Time)) return;
        RangeEnd = Combine(EndDatePicker.Date, EndTimePicker.Time);
    }

    void ApplySingleDate(DateTime date)
    {
        // Start of that day -> end of that day (23:59:59.9999999)
        var start = date.Date;
        var end = date.Date.AddDays(1).AddTicks(-1);

        SetPickersFromRange(start, end);
    }

    void ApplyPreset(string mode)
    {
        var now = DateTime.Now;
        DateTime start;
        DateTime end;

        switch (mode)
        {
            case "Today":
                start = now.Date;
                end = now.Date.AddDays(1).AddTicks(-1);
                break;

            case "This Week":
                // Week starts Monday (common in Ireland/ISO-8601)
                var delta = ((int)now.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
                start = now.Date.AddDays(-delta);
                end = start.AddDays(7).AddTicks(-1);
                break;

            case "This Month":
                start = new DateTime(now.Year, now.Month, 1);
                end = start.AddMonths(1).AddTicks(-1);
                break;

            case "This Year":
                start = new DateTime(now.Year, 1, 1);
                end = start.AddYears(1).AddTicks(-1);
                break;

            default:
                return;
        }

        SetPickersFromRange(start, end);
    }

    void SetPickersFromRange(DateTime start, DateTime end)
    {
        _suppress = true;
        try
        {
            RangeStart = start;
            RangeEnd = end;

            StartDatePicker.Date = start.Date;
            StartTimePicker.Time = start.TimeOfDay;

            EndDatePicker.Date = end.Date;
            EndTimePicker.Time = end.TimeOfDay;
        }
        finally
        {
            _suppress = false;
        }
    }

    static DateTime Combine(DateTime date, TimeSpan time)
        => date.Date + time;
}