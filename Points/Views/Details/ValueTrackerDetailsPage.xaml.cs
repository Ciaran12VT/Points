using System.Globalization;
using Points.Models;

namespace Points.Views.Details;

public partial class ValueTrackerDetailsPage : ContentPage
{
    private readonly ValueTrackerCardModel _model;
    private readonly Action<ValueTrackerCardModel> _onSaved;
    private readonly Action _onCancelled;

    public ValueTrackerDetailsPage(
        ValueTrackerCardModel model,
        Action<ValueTrackerCardModel> onSaved,
        Action onCancelled)
    {
        InitializeComponent();

        _model = model;
        _onSaved = onSaved;
        _onCancelled = onCancelled;

        BindingContext = _model;

        // Defaults
        if (_model.CreatedDate == default)
            _model.CreatedDate = DateTime.Today;

        // Schedule picker options
        UnitPicker.ItemsSource = new List<string> { "Minute", "Hour", "Day", "Week", "Month", "Year" };
        UnitPicker.SelectedItem = string.IsNullOrWhiteSpace(_model.ScheduleUnit) ? "Week" : _model.ScheduleUnit;

        EveryEntry.Text = (_model.ScheduleEvery <= 0 ? 1 : _model.ScheduleEvery).ToString(CultureInfo.InvariantCulture);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _onCancelled?.Invoke();
        await Shell.Current.Navigation.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        ErrorLabel.Text = "";

        // Title validation
        var title = (TitleEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Title is required.");
            return;
        }

        // Unit (optional, but keep trimmed)
        var unit = (UnitEntry.Text ?? "").Trim();

        // Schedule every
        if (!int.TryParse((EveryEntry.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var every) || every <= 0)
        {
            ShowError("Schedule frequency must be a positive whole number.");
            return;
        }

        var scheduleUnit = UnitPicker.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(scheduleUnit))
        {
            ShowError("Please choose a schedule unit.");
            return;
        }

        // Parse initial values
        var parsedValues = ParseValues(ValuesEditor.Text);

        // Commit into model
        _model.Title = title;
        _model.Unit = unit;

        _model.RangeStart = StartDatePicker.Date;

        _model.ScheduleEvery = every;
        _model.ScheduleUnit = scheduleUnit;

        if (parsedValues.Count > 0)
            _model.SetValues(parsedValues);

        // Done
        _onSaved?.Invoke(_model);
        await Shell.Current.Navigation.PopAsync();
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
    }

    private static List<double> ParseValues(string? text)
    {
        var result = new List<double>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // Split on commas/newlines
        var parts = text
            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

        foreach (var p in parts)
        {
            // Match your existing prompts: parse InvariantCulture
            if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                result.Add(v);
        }

        return result;
    }
}
