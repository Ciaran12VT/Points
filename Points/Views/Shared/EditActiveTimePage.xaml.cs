using Microsoft.Maui.Graphics.Text;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Shared;
using Points.Views.Shared;

namespace Points.Views.Shared;

public partial class EditActiveTimePage : ContentPage
{
    // Returns edited activities to the caller
    private readonly TaskCompletionSource<List<ActivityModel>> _tcs;
    private readonly IActivityService _activity;
    private readonly IUdmdService _udmd;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

    public EditActiveTimePage(
        List<ActivityModel> activity,
        TaskCompletionSource<List<ActivityModel>> tcs,
        IActivityService activityService,
        IUdmdService udmd,
        ITimeZoneService timeZoneService,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        _tcs = tcs ?? throw new ArgumentNullException(nameof(tcs));
        _activity = activityService ?? throw new ArgumentNullException(nameof(activityService));
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        if (activity is null)
            throw new ArgumentNullException(nameof(activity));

        var localActivity = activity
            .Select(ToEditorLocalActivity)
            .ToList();

        BindingContext = new EditActiveTimeViewModel(
            activity: localActivity,

            onSave: edited =>
            {
                _tcs.TrySetResult(edited.Select(ToUtcActivity).ToList());
                _ = _navigation.PopAsync();
            },

            pickDateTime: CreatePickDateTimeDelegate(),

            confirmDelete: (title, message) =>
            {
                // Used by the Delete ("X") button flow in the VM.
                return _dialogs.DisplayAlertAsync(title, message, "Delete", "Cancel");
            }
        );

        _ = LoadMetadataSummariesAsync();
    }

    private ActivityModel ToEditorLocalActivity(ActivityModel model)
    {
        return new ActivityModel
        {
            Id = model.Id,
            CardID = model.CardID,
            StartDate = ToEditorLocalDateTime(model.StartDate),
            EndDate = model.EndDate.HasValue ? ToEditorLocalDateTime(model.EndDate.Value) : null,
            RateName = model.RateName,
            ValuePerMinute = model.ValuePerMinute
        };
    }

    private ActivityModel ToUtcActivity(ActivityModel model)
    {
        return new ActivityModel
        {
            Id = model.Id,
            CardID = model.CardID,
            StartDate = _timeZoneService.ToUtcFromLocal(model.StartDate),
            EndDate = model.EndDate.HasValue ? _timeZoneService.ToUtcFromLocal(model.EndDate.Value) : null,
            RateName = model.RateName,
            ValuePerMinute = model.ValuePerMinute
        };
    }

    private DateTime ToEditorLocalDateTime(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? _timeZoneService.ToLocal(value)
            : StrictTimeSerializer.RequireWallClockDateTime(value);
    }

    private async Task LoadMetadataSummariesAsync()
    {
        if (BindingContext is not EditActiveTimeViewModel vm)
            return;

        foreach (var row in vm.Rows.Where(x => x.Id > 0))
        {
            var metadata = await _udmd.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.Activity, row.Id);
            if (metadata.Count == 0)
                continue;

            row.MetadataSummary = string.Join("  |  ", metadata.Select(x =>
                $"{x.FieldName}: {UdmdValueFormatter.ToDisplayString(x)}"));
        }
    }

    public enum ActiveBoundary
    {
        Start = 0,
        End = 1
    }

    private Func<EditActiveTimeRow, ActiveBoundary, Task<DateTime?>> CreatePickDateTimeDelegate()
    {
        return async (row, boundary) =>
        {
            if (row is null) return null;

            var initial = boundary == ActiveBoundary.Start
                ? row.Start
                : (row.End ?? row.Start);

            // Structural bounds (start < end)
            DateTime min;
            DateTime max;

            if (boundary == ActiveBoundary.Start)
            {
                min = DateTime.MinValue;
                max = row.End.HasValue
                    ? row.End.Value.AddSeconds(-1)
                    : DateTime.MaxValue;
            }
            else // Editing End
            {
                min = row.Start.AddSeconds(1);
                max = DateTime.MaxValue;
            }

            // Optional: You may clamp further using prev/next if desired
            // var prevNext = await _db.GetPreviousAndNextActivePeriodDateTimes(initial);
            // min = Max(min, prevNext?.Item1 ?? DateTime.MinValue);
            // max = Min(max, prevNext?.Item2 ?? DateTime.MaxValue);

            Func<DateTime, Task<string?>> validateAsync = async chosen =>
            {
                if (chosen < min || chosen > max)
                    return "That time is outside the allowed range.";

                var candidateStart = boundary == ActiveBoundary.Start ? chosen : row.Start;
                var candidateEnd = boundary == ActiveBoundary.End ? chosen : row.End;

                var candidateStartUtc = _timeZoneService.ToUtcFromLocal(candidateStart);
                var candidateEndUtc = candidateEnd.HasValue
                    ? _timeZoneService.ToUtcFromLocal(candidateEnd.Value)
                    : (DateTime?)null;

                var overlaps = await _activity.HasActivityOverlapAsync(
                    excludeActivityId: row.Id,
                    candidateStart: candidateStartUtc,
                    candidateEnd: candidateEndUtc);

                return overlaps
                    ? "Overlaps another activity block."
                    : null;
            };

            var title = boundary == ActiveBoundary.Start
                ? "Edit start time"
                : "Edit end time";

            return await DateTimePickerSheet.PickAsync(
                page: this,
                navigation: _navigation,
                initial: initial,
                min: min,
                max: max,
                validateAsync: validateAsync,
                title: title);
        };
    }
}
