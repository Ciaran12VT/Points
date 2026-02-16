using System.Globalization;
using Points.Models; // expects CardSchedule + FrequencyType

namespace Points.Views.Schedules;

public partial class ScheduleEditPage : ContentPage
{
    private readonly CardSchedule _schedule;
    private readonly Func<CardSchedule, Task> _onSaved;

    public ScheduleEditPage(CardSchedule schedule, Func<CardSchedule, Task> onSaved)
    {
        InitializeComponent();

        _schedule = schedule;
        _onSaved = onSaved;

        // Populate frequency picker
        var values = Enum.GetValues(typeof(FrequencyType)).Cast<FrequencyType>().ToList();
        FrequencyTypePicker.ItemsSource = values.Select(ToFriendly).ToList();

        // Set selected
        FrequencyTypePicker.SelectedIndex = values.IndexOf(_schedule.FrequencyType);

        // Seed date/time
        FromDatePicker.Date = _schedule.FromDateTime.Date;
        FromTimePicker.Time = _schedule.FromDateTime.TimeOfDay;

        // Enabled + Note
        EnabledSwitch.IsToggled = _schedule.IsEnabled;
        NoteEditor.Text = _schedule.Note ?? "";

        if (_schedule.ToDateTime.HasValue)
        {
            HasEndSwitch.IsToggled = true;
            ToRow.IsVisible = true;
            ToDatePicker.Date = _schedule.ToDateTime.Value.Date;
            ToTimePicker.Time = _schedule.ToDateTime.Value.TimeOfDay;
        }
        else
        {
            HasEndSwitch.IsToggled = false;
            ToRow.IsVisible = false;
            ToDatePicker.Date = DateTime.Now.Date;
            ToTimePicker.Time = new TimeSpan(9, 0, 0);
        }

        FrequencyValueEntry.Text = (_schedule.FrequencyValue <= 0 ? 1 : _schedule.FrequencyValue)
            .ToString(CultureInfo.InvariantCulture);

        // Wire events
        FrequencyTypePicker.SelectedIndexChanged += (_, __) => OnFrequencyChanged(values);
        HasEndSwitch.Toggled += (_, __) => { ToRow.IsVisible = HasEndSwitch.IsToggled; UpdatePreview(values); };

        FromDatePicker.DateSelected += (_, __) => UpdatePreview(values);
        FromTimePicker.PropertyChanged += (_, __) => UpdatePreview(values);
        ToDatePicker.DateSelected += (_, __) => UpdatePreview(values);
        ToTimePicker.PropertyChanged += (_, __) => UpdatePreview(values);
        FrequencyValueEntry.TextChanged += (_, __) => UpdatePreview(values);

        // Initial state
        OnFrequencyChanged(values);
        UpdatePreview(values);
    }

    private void OnFrequencyChanged(List<FrequencyType> allValues)
    {
        var ft = GetSelectedFrequency(allValues);

        // Only show FrequencyValue for EveryDays
        FrequencyValueRow.IsVisible = ft == FrequencyType.EveryDays;

        // If switching away, keep value but ignore it.
        UpdatePreview(allValues);
    }

    private FrequencyType GetSelectedFrequency(List<FrequencyType> allValues)
    {
        var idx = FrequencyTypePicker.SelectedIndex;
        if (idx < 0 || idx >= allValues.Count) return FrequencyType.Once;
        return allValues[idx];
    }

    private void UpdatePreview(List<FrequencyType> allValues)
    {
        var ft = GetSelectedFrequency(allValues);
        var from = Combine(FromDatePicker.Date, FromTimePicker.Time);

        var hasEnd = HasEndSwitch.IsToggled;
        var to = hasEnd ? Combine(ToDatePicker.Date, ToTimePicker.Time) : (DateTime?)null;

        var every = ParsePositiveInt(FrequencyValueEntry.Text, fallback: 1);

        var preview = FormatPreview(ft, every, from, to);
        PreviewLabel.Text = preview;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PopModalAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        ErrorLabel.Text = "";

        // Build
        var allValues = Enum.GetValues(typeof(FrequencyType)).Cast<FrequencyType>().ToList();
        var ft = GetSelectedFrequency(allValues);

        var from = Combine(FromDatePicker.Date, FromTimePicker.Time);

        DateTime? to = null;
        if (HasEndSwitch.IsToggled)
            to = Combine(ToDatePicker.Date, ToTimePicker.Time);

        var every = ParsePositiveInt(FrequencyValueEntry.Text, fallback: 1);

        // Validate
        if (HasEndSwitch.IsToggled && to.HasValue && to.Value < from)
        {
            ShowError("End date/time must be after the start date/time.");
            return;
        }

        if (ft == FrequencyType.EveryDays && every <= 0)
        {
            ShowError("Frequency must be a positive whole number.");
            return;
        }

        // Commit to schedule
        _schedule.FrequencyType = ft;
        _schedule.FrequencyValue = (ft == FrequencyType.EveryDays) ? every : 0;
        _schedule.FromDateTime = from;
        _schedule.ToDateTime = HasEndSwitch.IsToggled ? to : null;
        _schedule.IsEnabled = EnabledSwitch.IsToggled;
        _schedule.Note = (NoteEditor.Text ?? "").Trim();

        // Save callback
        await _onSaved(_schedule);

        // Close
        await Shell.Current.Navigation.PopModalAsync();
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
    }

    private static DateTime Combine(DateTime date, TimeSpan time)
        => date.Date.Add(time);

    private static int ParsePositiveInt(string? text, int fallback)
    {
        if (int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0)
            return v;
        return fallback;
    }

    private static string ToFriendly(FrequencyType ft) => ft switch
    {
        FrequencyType.Once => "Once",
        FrequencyType.EveryDays => "Every N days",
        FrequencyType.EveryWeekday => "Every weekday",
        FrequencyType.EveryMonday => "Every Monday",
        FrequencyType.EveryTuesday => "Every Tuesday",
        FrequencyType.EveryWednesday => "Every Wednesday",
        FrequencyType.EveryThursday => "Every Thursday",
        FrequencyType.EveryFriday => "Every Friday",
        FrequencyType.EverySaturday => "Every Saturday",
        FrequencyType.EverySunday => "Every Sunday",
        FrequencyType.EveryWeeks => "Every week",
        FrequencyType.EveryMonths => "Every month",
        FrequencyType.EveryYears => "Every year",
        _ => ft.ToString()
    };

    private static string FormatPreview(FrequencyType ft, int every, DateTime from, DateTime? to)
    {
        var t = from.ToString("HH:mm");
        var start = from.ToString("yyyy-MM-dd");
        var end = to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "Never";

        var freq = ft switch
        {
            FrequencyType.Once => $"Once at {t}",
            FrequencyType.EveryDays => $"Every {Math.Max(1, every)} day(s) at {t}",
            FrequencyType.EveryWeekday => $"Every weekday at {t}",
            FrequencyType.EveryMonday => $"Every Monday at {t}",
            FrequencyType.EveryTuesday => $"Every Tuesday at {t}",
            FrequencyType.EveryWednesday => $"Every Wednesday at {t}",
            FrequencyType.EveryThursday => $"Every Thursday at {t}",
            FrequencyType.EveryFriday => $"Every Friday at {t}",
            FrequencyType.EverySaturday => $"Every Saturday at {t}",
            FrequencyType.EverySunday => $"Every Sunday at {t}",
            FrequencyType.EveryWeeks => $"Every week at {t}",
            FrequencyType.EveryMonths => $"Every month at {t}",
            FrequencyType.EveryYears => $"Every year at {t}",
            _ => ft.ToString()
        };

        return $"{freq}  ·  From: {start}  ·  Ends: {end}";
    }
}
