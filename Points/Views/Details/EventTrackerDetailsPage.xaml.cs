using System.Globalization;
using Points.Models;

namespace Points.Views.Details;

public partial class EventTrackerDetailsPage : ContentPage
{
    private readonly EventTrackerCardModel _model;
    private readonly Action<EventTrackerCardModel> _onSaved;
    private readonly Func<EventTrackerCardModel, Task> _onDelete;
    private readonly Action _onCancelled;

    // This exists only to bind DatePicker cleanly, same as your ValueTracker page pattern.
    public DateTime StartDate { get; set; }

    public EventTrackerDetailsPage(
        EventTrackerCardModel model,
        Action<EventTrackerCardModel> onSaved,
        Func<EventTrackerCardModel, Task> onDelete,
        Action onCancelled)
    {
        InitializeComponent();

        _model = model;
        _onSaved = onSaved;
        _onDelete = onDelete;
        _onCancelled = onCancelled;

        // Defaults
        if (_model.CreatedDate == default)
            _model.CreatedDate = DateTime.Today;

        if (_model.RangeStart == default)
            _model.RangeStart = DateTime.Today;

        StartDate = _model.RangeStart;

        BindingContext = this; // we bind Title manually from entry; or bind to _model + set Date separately

        // If you prefer BindingContext = _model like the Value page, do this:
        // BindingContext = _model;
        // and set StartDatePicker.Date = _model.RangeStart;
        // But the XAML currently binds Date="{Binding StartDate}", so we use BindingContext=this.

        // Group by options
        GroupByPicker.ItemsSource = new List<string> { "Day", "Week", "Month", "Year" };
        GroupByPicker.SelectedItem = string.IsNullOrWhiteSpace(_model.GroupByPeriod) ? "Day" : _model.GroupByPeriod;

        // Title initial value
        TitleEntry.Text = _model.Title ?? "";
        StartDatePicker.Date = StartDate;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _onCancelled?.Invoke();
        await Shell.Current.Navigation.PopAsync();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Delete Arc?",
            "This will delete this Arc and its saved values. Continue?",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        try
        {
            if (_onDelete != null)
                await _onDelete(_model);

            await Shell.Current.Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Delete failed", ex.Message, "OK");
        }
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        ErrorLabel.Text = "";

        var title = (TitleEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Title is required.");
            return;
        }

        var groupBy = GroupByPicker.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            ShowError("Please choose an aggregate period.");
            return;
        }

        var start = StartDatePicker.Date;

        // Parse initial events (optional)
        var parsedEventTimes = ParseEventTimes(EventsEditor.Text);

        // Commit into model
        _model.Title = title;
        _model.GroupByPeriod = groupBy;
        _model.RangeStart = start;

        if (parsedEventTimes.Count > 0)
            _model.SetValues(parsedEventTimes);

        _onSaved?.Invoke(_model);
        await Shell.Current.Navigation.PopAsync();
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
    }

    private static List<DateTime> ParseEventTimes(string? text)
    {
        var result = new List<DateTime>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var parts = text
            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

        // Accept a few friendly formats, plus invariant parsing.
        // You can tighten this later if you want.
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd H:mm",
            "yyyy-MM-dd",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy",
            "MM/dd/yyyy HH:mm",
            "MM/dd/yyyy",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "o"
        };

        foreach (var p in parts)
        {
            if (DateTime.TryParseExact(p, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var dt) ||
                DateTime.TryParse(p, CultureInfo.CurrentCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out dt) ||
                DateTime.TryParse(p, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out dt))
            {
                result.Add(dt);
            }
        }

        // Keep them ordered
        result.Sort();
        return result;
    }
}
