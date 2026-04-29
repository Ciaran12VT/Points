using System.Globalization;
using Points.Helpers;
using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.Views.Schedules;

namespace Points.Views.Details;

public partial class ValueTrackerDetailsPage : ContentPage
{
    private readonly ValueTrackerCardModel _model;
    private readonly Action<ValueTrackerCardModel> _onSaved;
    private readonly Func<ValueTrackerCardModel, Task> _onDelete;
    private readonly Action _onCancelled;
    private readonly IUdmdService _udmd;

    public ValueTrackerDetailsPage(
        ValueTrackerCardModel model,
        Action<ValueTrackerCardModel> onSaved,
        Func<ValueTrackerCardModel, Task> onDelete,
        Action onCancelled,
        IUdmdService udmd)
    {
        InitializeComponent();

        _model = model;
        _onSaved = onSaved;
        _onDelete = onDelete;
        _onCancelled = onCancelled;
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));

        BindingContext = _model;

        var clock = ServiceHelper.GetService<IClock>();

        // Defaults
        if (_model.CreatedDate == default)
            _model.CreatedDate = clock.LocalNow.Date;

        // Schedule picker options
        //UnitPicker.ItemsSource = new List<string> { "Minute", "Hour", "Day", "Week", "Month", "Year" };
        //UnitPicker.SelectedItem = string.IsNullOrWhiteSpace(_model.ScheduleUnit) ? "Week" : _model.ScheduleUnit;

        //EveryEntry.Text = (_model.ScheduleEvery <= 0 ? 1 : _model.ScheduleEvery).ToString(CultureInfo.InvariantCulture);

        // For now: schedule summary placeholder (until schedules are implemented)
        ScheduleSummaryLabel.Text =
            _model.Schedules.Count == 0 ? "None" :
            _model.Schedules.Count == 1 ? "1 schedule" :
            $"{_model.Schedules.Count} schedules";

        _ = LoadMetadataHistoryAsync();
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
        //if (!int.TryParse((EveryEntry.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var every) || every <= 0)
        //{
        //    ShowError("Schedule frequency must be a positive whole number.");
        //    return;
        //}

        //var scheduleUnit = UnitPicker.SelectedItem as string;
        //if (string.IsNullOrWhiteSpace(scheduleUnit))
        //{
        //    ShowError("Please choose a schedule unit.");
        //    return;
        //}

        // Parse initial values
        var parsedValues = ParseValues(ValuesEditor.Text);

        // Commit into model
        _model.Title = title;
        _model.Unit = unit;

        //_model.RangeStart = StartDatePicker.Date;

        //_model.ScheduleEvery = every;
        //_model.ScheduleUnit = scheduleUnit;

        if (parsedValues.Count > 0)
            _model.SetValues(parsedValues);

        // Done
        _onSaved?.Invoke(_model);
        await Shell.Current.Navigation.PopAsync();
    }

    private async void OnEditSchedulesClicked(object sender, EventArgs e)
    {
        // Require a persisted card so schedules can be keyed by CardId
        if (_model.Id <= 0)
        {
            ShowError("Please tap OK to save the tracker first, then add schedules.");
            return;
        }

        // For now, delegates are null (in-memory-only UI).
        // We'll wire these to DB repository methods next.
        await Shell.Current.Navigation.PushAsync(
            new CardSchedulesPage(
                cardId: _model.Id,
                schedules: _model.Schedules,
                onChanged: () =>
                {
                    // simplest summary update (you can improve formatting later)
                    ScheduleSummaryLabel.Text = _model.Schedules.Count == 0 ? "None"
                        : _model.Schedules.Count == 1 ? "1 schedule"
                        : $"{_model.Schedules.Count} schedules";
                }));

    }

    private async void OnEditUdmdClicked(object sender, EventArgs e)
    {
        if (_model.CardID <= 0)
        {
            ShowError("Please save the tracker before configuring metadata fields.");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new UdmdConfigPage(_model.CardID, _udmd));
    }

    private async Task LoadMetadataHistoryAsync()
    {
        if (_model.Values.Count == 0)
            return;

        MetadataHistoryStack.Children.Clear();

        foreach (var value in _model.Values.Where(x => x.Id > 0).OrderByDescending(x => x.Timestamp))
        {
            var metadata = await _udmd.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.TrackerValue, value.Id);
            if (metadata.Count == 0)
                continue;

            MetadataHistoryStack.Children.Add(new Label
            {
                Text = $"{FormatTimestamp(value.Timestamp)}: {FormatMetadata(metadata)}",
                FontSize = 13,
                Opacity = 0.8
            });
        }

        var hasMetadata = MetadataHistoryStack.Children.Count > 0;
        MetadataHistoryTitle.IsVisible = hasMetadata;
        MetadataHistoryStack.IsVisible = hasMetadata;
    }

    private static string FormatMetadata(IEnumerable<UdmdTransModel> metadata)
    {
        return string.Join("  |  ", metadata.Select(x =>
            $"{x.FieldName}: {UdmdValueFormatter.ToDisplayString(x)}"));
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        return TimeDisplayFormatter.FormatInstant(timestamp, "MMM-dd HH:mm");
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
