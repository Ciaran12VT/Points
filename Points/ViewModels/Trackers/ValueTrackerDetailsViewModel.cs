using System.Collections.ObjectModel;
using Points.ViewModels.Shared;
using System.Globalization;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Trackers;

public sealed class ValueTrackerDetailsViewModel : Models.ObservableObject
{
    private readonly ValueTrackerCardModel _model;
    private readonly Action<ValueTrackerCardModel> _onSaved;
    private readonly Func<ValueTrackerCardModel, Task> _onDelete;
    private readonly Action _onCancelled;
    private readonly IUdmdService _udmd;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly ActiveCardDetailsInteractionCoordinator _detailsInteractions;

    public Command CancelCommand { get; }
    public Command DeleteCommand { get; }
    public Command SaveCommand { get; }
    public Command EditSchedulesCommand { get; }
    public Command EditUdmdCommand { get; }

    public ObservableCollection<string> MetadataHistoryRows { get; } = new();

    public bool HasMetadataHistory => MetadataHistoryRows.Count > 0;

    public string ScheduleSummaryText => FormatCount(_model.Schedules.Count, "schedule");

    private string _title = "";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _unit = "";
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    private DateTime _startDate;
    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    private string _initialValuesText = "";
    public string InitialValuesText
    {
        get => _initialValuesText;
        set => SetProperty(ref _initialValuesText, value);
    }

    private string _errorText = "";
    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
                RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public ValueTrackerDetailsViewModel(
        ValueTrackerCardModel model,
        Action<ValueTrackerCardModel> onSaved,
        Func<ValueTrackerCardModel, Task> onDelete,
        Action onCancelled,
        IUdmdService udmd,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _onSaved = onSaved ?? throw new ArgumentNullException(nameof(onSaved));
        _onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
        _onCancelled = onCancelled ?? throw new ArgumentNullException(nameof(onCancelled));
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _detailsInteractions = new ActiveCardDetailsInteractionCoordinator(_navigation, _dialogs, clock: clock);

        if (_model.CreatedDate == default)
            _model.CreatedDate = clock.LocalNow.Date;

        Title = _model.Title;
        Unit = _model.Unit;
        StartDate = _model.CreatedDate.Date;

        CancelCommand = new Command(async () => await CancelAsync());
        DeleteCommand = new Command(async () => await DeleteAsync());
        SaveCommand = new Command(async () => await SaveAsync());
        EditSchedulesCommand = new Command(async () => await EditSchedulesAsync());
        EditUdmdCommand = new Command(async () => await EditUdmdAsync());

        _ = LoadMetadataHistoryAsync();
    }

    private async Task CancelAsync()
    {
        _onCancelled.Invoke();
        await _navigation.PopAsync();
    }

    private async Task DeleteAsync()
    {
        var confirmed = await _dialogs.DisplayAlertAsync(
            "Delete Arc?",
            "This will delete this Arc and its saved values. Continue?",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        try
        {
            await _onDelete(_model);
            await _navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Delete failed", ex.Message, "OK");
        }
    }

    private async Task SaveAsync()
    {
        ClearError();

        var title = (Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Title is required.");
            return;
        }

        _model.Title = title;
        _model.Unit = (Unit ?? "").Trim();

        var parsedValues = ParseValues(InitialValuesText);
        if (parsedValues.Count > 0)
            _model.SetValues(parsedValues);

        _onSaved.Invoke(_model);
        await _navigation.PopAsync();
    }

    private async Task EditSchedulesAsync()
    {
        ClearError();

        await _detailsInteractions.EditSchedulesAsync(
            _model.Id,
            _model.Schedules,
            RefreshScheduleSummary,
            ShowError,
            "Please tap OK to save the tracker first, then add schedules.");
    }

    private async Task EditUdmdAsync()
    {
        ClearError();

        await _detailsInteractions.EditUdmdAsync(
            _model.CardID,
            _udmd,
            ShowError,
            "Please save the tracker before configuring metadata fields.");
    }

    private async Task LoadMetadataHistoryAsync()
    {
        if (_model.Values.Count == 0)
            return;

        MetadataHistoryRows.Clear();

        foreach (var value in _model.Values.Where(x => x.Id > 0).OrderByDescending(x => x.Timestamp))
        {
            var metadata = await _udmd.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.TrackerValue, value.Id);
            if (metadata.Count == 0)
                continue;

            MetadataHistoryRows.Add($"{FormatTimestamp(value.Timestamp)}: {FormatMetadata(metadata)}");
        }

        RaisePropertyChanged(nameof(HasMetadataHistory));
    }

    private void RefreshScheduleSummary()
    {
        RaisePropertyChanged(nameof(ScheduleSummaryText));
    }

    private void ShowError(string message)
    {
        ErrorText = message;
    }

    private void ClearError()
    {
        ErrorText = "";
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

    private static List<double> ParseValues(string? text)
    {
        var result = new List<double>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var parts = text
            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

        foreach (var part in parts)
        {
            if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                result.Add(value);
        }

        return result;
    }

    private static string FormatCount(int count, string singular)
    {
        if (count == 0)
            return "None";

        return count == 1 ? $"1 {singular}" : $"{count} {singular}s";
    }
}
