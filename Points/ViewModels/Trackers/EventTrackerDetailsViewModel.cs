using System.Collections.ObjectModel;
using Points.ViewModels.Shared;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Trackers;

public sealed class EventTrackerDetailsViewModel : Models.ObservableObject
{
    private static readonly IReadOnlyList<string> DefaultGroupByOptions =
    new[]
    {
        "Day",
        "Week",
        "Month",
        "Year"
    };

    private readonly EventTrackerCardModel _model;
    private readonly Action<EventTrackerCardModel> _onSaved;
    private readonly Func<EventTrackerCardModel, Task> _onDelete;
    private readonly Func<EventTrackerCardModel, Task<bool>> _wouldArchiveOnDelete;
    private readonly Action _onCancelled;
    private readonly IUdmdService _udmd;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly ActiveCardDetailsInteractionCoordinator _detailsInteractions;

    public Command CancelCommand { get; }
    public Command DeleteCommand { get; }
    public Command SaveCommand { get; }
    public Command EditUdmdCommand { get; }

    public IReadOnlyList<string> GroupByOptions => DefaultGroupByOptions;

    public ObservableCollection<string> MetadataHistoryRows { get; } = new();

    public bool HasMetadataHistory => MetadataHistoryRows.Count > 0;

    private string _title = "";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private DateTime _startDate;
    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    private string _selectedGroupByPeriod = "Day";
    public string SelectedGroupByPeriod
    {
        get => _selectedGroupByPeriod;
        set => SetProperty(ref _selectedGroupByPeriod, value);
    }

    private string _initialEventsText = "";
    public string InitialEventsText
    {
        get => _initialEventsText;
        set => SetProperty(ref _initialEventsText, value);
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

    public EventTrackerDetailsViewModel(
        EventTrackerCardModel model,
        Action<EventTrackerCardModel> onSaved,
        Func<EventTrackerCardModel, Task> onDelete,
        Func<EventTrackerCardModel, Task<bool>> wouldArchiveOnDelete,
        Action onCancelled,
        IUdmdService udmd,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _onSaved = onSaved ?? throw new ArgumentNullException(nameof(onSaved));
        _onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
        _wouldArchiveOnDelete = wouldArchiveOnDelete ?? throw new ArgumentNullException(nameof(wouldArchiveOnDelete));
        _onCancelled = onCancelled ?? throw new ArgumentNullException(nameof(onCancelled));
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _detailsInteractions = new ActiveCardDetailsInteractionCoordinator(_navigation, _dialogs, clock: clock);

        if (_model.CreatedDate == default)
            _model.CreatedDate = clock.LocalNow.Date;

        if (_model.RangeStart == default)
            _model.RangeStart = clock.LocalNow.Date;

        Title = _model.Title ?? "";
        StartDate = _model.RangeStart.Date;
        SelectedGroupByPeriod = string.IsNullOrWhiteSpace(_model.GroupByPeriod)
            ? "Day"
            : _model.GroupByPeriod;

        CancelCommand = new Command(async () => await CancelAsync());
        DeleteCommand = new Command(async () => await DeleteAsync());
        SaveCommand = new Command(async () => await SaveAsync());
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
        var deleteActionText = await GetDeleteActionTextAsync();
        var confirmed = await _dialogs.DisplayAlertAsync(
            $"{deleteActionText} Arc?",
            deleteActionText == "Archive"
                ? "This Arc has saved values, so it will be archived and kept for reporting."
                : "This Arc has no saved values, so it will be deleted.",
            deleteActionText,
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

    private async Task<string> GetDeleteActionTextAsync()
    {
        return await _wouldArchiveOnDelete(_model)
            ? "Archive"
            : "Delete";
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

        var groupBy = SelectedGroupByPeriod;
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            ShowError("Please choose an aggregate period.");
            return;
        }

        var parsedEventTimes = ParseEventTimes(InitialEventsText);

        _model.Title = title;
        _model.GroupByPeriod = groupBy;
        _model.RangeStart = StartDate.Date;

        if (parsedEventTimes.Count > 0)
            _model.SetValues(parsedEventTimes);

        _onSaved.Invoke(_model);
        await _navigation.PopAsync();
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
        MetadataHistoryRows.Clear();

        if (_model.Values.Count == 0)
        {
            RaisePropertyChanged(nameof(HasMetadataHistory));
            return;
        }

        foreach (var value in _model.Values.Where(x => x.Id > 0).OrderByDescending(x => x.Timestamp))
        {
            var metadata = await _udmd.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.TrackerValue, value.Id);
            if (metadata.Count == 0)
                continue;

            MetadataHistoryRows.Add($"{FormatTimestamp(value.Timestamp)}: {FormatMetadata(metadata)}");
        }

        RaisePropertyChanged(nameof(HasMetadataHistory));
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

    private static List<DateTime> ParseEventTimes(string? text)
    {
        var result = new List<DateTime>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var parts = text
            .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

        foreach (var part in parts)
        {
            if (LegacyTimeReader.TryReadLocalDateTime(part, out var timestamp) &&
                timestamp is not null)
                result.Add(timestamp.LocalDateTime);
        }

        result.Sort();
        return result;
    }
}
