using Points.Models;
using Points.Services.MissionSharing;
using Points.Services.Navigation;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Points.ViewModels.Missions;

public sealed class MissionImportViewModel : ObservableObject
{
    private readonly MissionSharePreview _preview;
    private readonly IMissionShareService _missionShares;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

    private bool _isBusy;

    public MissionImportViewModel(
        MissionSharePreview preview,
        IMissionShareService missionShares,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _missionShares = missionShares ?? throw new ArgumentNullException(nameof(missionShares));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        AcceptCommand = new Command(async () => await AcceptAsync(), () => !IsBusy);
        RejectCommand = new Command(async () => await RejectAsync(), () => !IsBusy);
        DiffItems = preview.DiffItems;
    }

    public Command AcceptCommand { get; }
    public Command RejectCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            AcceptCommand.ChangeCanExecute();
            RejectCommand.ChangeCanExecute();
        }
    }

    public string PageTitle => _preview.IsUpdate ? "Mission Update" : "Shared Mission";
    public string IntroText => _preview.IsUpdate
        ? "This will update an existing mission."
        : "This will import a new mission.";

    public bool IsUpdate => _preview.IsUpdate;
    public bool HasDiffItems => DiffItems.Count > 0;
    public ObservableCollection<MissionShareDiffItem> DiffItems { get; }

    public string SharedByText => string.IsNullOrWhiteSpace(_preview.Envelope.SharedBy)
        ? "Unknown"
        : _preview.Envelope.SharedBy;

    public string TitleText => _preview.IncomingMission.Title;
    public string StatusText => _preview.IncomingMission.Status;
    public string TagsText => string.IsNullOrWhiteSpace(_preview.IncomingMission.Tags) ? "--" : _preview.IncomingMission.Tags;
    public string DescriptionText => string.IsNullOrWhiteSpace(_preview.IncomingMission.Description) ? "--" : _preview.IncomingMission.Description;
    public string SubTypeText => _preview.IncomingMission.SubType.ToString();
    public string ValueText => _preview.IncomingMission.Value.ToString("0.##", CultureInfo.InvariantCulture);
    public string ValuePerMinuteText => _preview.IncomingMission.ValuePerMinute.ToString("0.##", CultureInfo.InvariantCulture);
    public string AvailableText => FormatDate(_preview.IncomingMission.AvailableFromDate);
    public string DueText => FormatDate(_preview.IncomingMission.DueDate);
    public string EventText => FormatDate(_preview.IncomingMission.EventDate);
    public string CompletedText => FormatDate(_preview.IncomingMission.CompletedDate);
    public string EstimatedTimeText => _preview.IncomingMission.EstCompletionTimeText;

    private async Task AcceptAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            await _missionShares.AcceptImportAsync(_preview);

            await _dialogs.DisplayAlertAsync(
                _preview.IsUpdate ? "Mission updated" : "Mission imported",
                _preview.IsUpdate ? "The shared mission update has been applied." : "The shared mission has been added.",
                "OK");

            await _navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Import failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RejectAsync()
    {
        await _navigation.PopAsync();
    }

    private static string FormatDate(DateTime value)
    {
        return value == default
            ? "--"
            : value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? FormatDate(value.Value) : "--";
    }
}
