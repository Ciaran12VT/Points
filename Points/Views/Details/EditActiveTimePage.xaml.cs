using Microsoft.Maui.Graphics.Text;
using Points.Helpers;
using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;
using Points.Views.Shared;

namespace Points.Views.Details;

public partial class EditActiveTimePage : ContentPage
{
    // Returns edited activities to the caller
    private readonly TaskCompletionSource<List<ActivityModel>> _tcs;
    private readonly IDbService _db;
    private readonly ITimeZoneService _timeZoneService;

    public EditActiveTimePage(List<ActivityModel> activity, TaskCompletionSource<List<ActivityModel>> tcs, IDbService db, ITimeZoneService? timeZoneService = null)
    {
        InitializeComponent();

        _tcs = tcs ?? throw new ArgumentNullException(nameof(tcs));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeZoneService = timeZoneService ?? ResolveTimeZoneService();

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
                _ = Navigation.PopAsync();
            },

            pickDateTime: CreatePickDateTimeDelegate(),

            confirmDelete: (title, message) =>
            {
                // Used by the Delete ("X") button flow in the VM.
                return DisplayAlert(title, message, "Delete", "Cancel");
            }
        );

        _ = LoadMetadataSummariesAsync();
    }

    private static ITimeZoneService ResolveTimeZoneService()
    {
        try
        {
            var service = ServiceHelper.GetService<ITimeZoneService>();
            if (service != null)
                return service;
        }
        catch
        {
        }

        return new TimeZoneService();
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
            var metadata = await _db.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.Activity, row.Id);
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

                var overlaps = await _db.HasActivityOverlapAsync(
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
                initial: initial,
                min: min,
                max: max,
                validateAsync: validateAsync,
                title: title);
        };
    }
}

//internal static class DateTimePickerSheet
//{
//    /// <summary>
//    /// Shows a modal date+time picker, enforcing:
//    /// - hard bounds: chosen must be within [min, max]
//    /// - optional async validation: validateAsync returns null/empty => valid, otherwise an error message
//    /// </summary>
//    public static Task<DateTime?> PickAsync(
//        Page page,
//        DateTime initial,
//        DateTime min,
//        DateTime max,
//        Func<DateTime, Task<string?>>? validateAsync = null,
//        string title = "Edit")
//    {
//        if (page is null) throw new ArgumentNullException(nameof(page));
//        if (min > max) throw new ArgumentException("min must be <= max");

//        var tcs = new TaskCompletionSource<DateTime?>();

//        // Clamp initial into bounds so the modal never opens invalid
//        initial = Clamp(initial, min, max);

//        var datePicker = new DatePicker
//        {
//            Date = initial.Date,
//            HorizontalOptions = LayoutOptions.Fill
//        };

//        // Note: DatePicker bounds are DATE-only, so we apply full DateTime bounds in validation
//        if (min != DateTime.MinValue) datePicker.MinimumDate = min.Date;
//        if (max != DateTime.MaxValue) datePicker.MaximumDate = max.Date;

//        var timePicker = new TimePicker
//        {
//            Time = initial.TimeOfDay,
//            HorizontalOptions = LayoutOptions.Fill
//        };

//        var validationLabel = new Label
//        {
//            Text = "",
//            TextColor = Colors.Red,
//            IsVisible = false
//        };

//        var ok = new Button
//        {
//            Text = "OK",
//            BackgroundColor = Colors.Green,
//            TextColor = Colors.White,
//            FontAttributes = FontAttributes.Bold,
//            FontSize = 21f,
//            HeightRequest = 48,
//            CornerRadius = 12,
//            IsEnabled = false
//        };

//        var cancel = new Button
//        {
//            Text = "Cancel",
//            BackgroundColor = Colors.Gray,
//            TextColor = Colors.White,
//            FontAttributes = FontAttributes.Bold,
//            FontSize = 21f,
//            HeightRequest = 48,
//            CornerRadius = 12
//        };

//        DateTime GetChosen() => datePicker.Date + timePicker.Time;

//        // Debounce + stale-result protection
//        CancellationTokenSource? debounceCts = null;
//        int validationVersion = 0;

//        void SetError(string message)
//        {
//            ok.IsEnabled = false;
//            validationLabel.IsVisible = true;
//            validationLabel.Text = message;
//        }

//        void ClearError()
//        {
//            ok.IsEnabled = true;
//            validationLabel.IsVisible = false;
//            validationLabel.Text = "";
//        }

//        async Task ValidateAndUpdateUiAsync()
//        {
//            var chosen = GetChosen();

//            // Hard bounds first (fast)
//            if (chosen < min || chosen > max)
//            {
//                SetError($"Pick a time between {FormatBound(min)} and {FormatBound(max)}.");
//                return;
//            }

//            // No async validator => valid if in range
//            if (validateAsync is null)
//            {
//                ClearError();
//                return;
//            }

//            // Debounce rapid changes (especially TimePicker)
//            debounceCts?.Cancel();
//            debounceCts?.Dispose();
//            debounceCts = new CancellationTokenSource();
//            var token = debounceCts.Token;

//            var myVersion = ++validationVersion;

//            try
//            {
//                // small delay to avoid hammering the validator while user scrolls
//                await Task.Delay(150, token);

//                // If cancelled during the delay, stop
//                token.ThrowIfCancellationRequested();

//                var error = await validateAsync(chosen);

//                // Ignore stale validation results
//                if (myVersion != validationVersion) return;

//                if (string.IsNullOrWhiteSpace(error))
//                    ClearError();
//                else
//                    SetError(error);
//            }
//            catch (OperationCanceledException)
//            {
//                // ignore - user changed value again
//            }
//            catch (Exception ex)
//            {
//                // Defensive: block OK if validation itself fails
//                SetError($"Validation error: {ex.Message}");
//            }
//        }

//        // Build modal page
//        var modal = new ContentPage
//        {
//            Title = title,
//            Content = new VerticalStackLayout
//            {
//                Padding = 16,
//                Spacing = 12,
//                Children =
//                    {
//                        new Label { Text = "Date", FontAttributes = FontAttributes.Bold },
//                        datePicker,
//                        new Label { Text = "Time", FontAttributes = FontAttributes.Bold },
//                        timePicker,
//                        validationLabel,
//                        new Grid
//                        {
//                            ColumnDefinitions =
//                            {
//                                new ColumnDefinition { Width = GridLength.Star },
//                                new ColumnDefinition { Width = GridLength.Star }
//                            },
//                            ColumnSpacing = 12,
//                            Children = { cancel, ok }
//                        }
//                    }
//            }
//        };

//        Grid.SetColumn(cancel, 0);
//        Grid.SetColumn(ok, 1);

//        // Important: only set null result if nothing else has set the TCS
//        modal.Disappearing += (_, __) => tcs.TrySetResult(null);

//        cancel.Clicked += async (_, __) =>
//        {
//            tcs.TrySetResult(null);
//            await page.Navigation.PopModalAsync();
//        };

//        ok.Clicked += async (_, __) =>
//        {
//            // Re-validate once more before accepting (covers edge cases / races)
//            await ValidateAndUpdateUiAsync();
//            if (!ok.IsEnabled) return;

//            tcs.TrySetResult(GetChosen());
//            await page.Navigation.PopModalAsync();
//        };

//        // Validate whenever user changes date/time
//        datePicker.DateSelected += (_, __) => _ = ValidateAndUpdateUiAsync();

//        timePicker.PropertyChanged += (_, e) =>
//        {
//            if (e.PropertyName == TimePicker.TimeProperty.PropertyName)
//                _ = ValidateAndUpdateUiAsync();
//        };

//        // Initial validation
//        _ = ValidateAndUpdateUiAsync();

//        _ = page.Navigation.PushModalAsync(new NavigationPage(modal));
//        return tcs.Task;
//    }

//    private static DateTime Clamp(DateTime value, DateTime min, DateTime max)
//    {
//        if (value < min) return min;
//        if (value > max) return max;
//        return value;
//    }

//    private static string FormatBound(DateTime dt)
//    {
//        if (dt == DateTime.MinValue) return "the beginning of time";
//        if (dt == DateTime.MaxValue) return "the end of time";
//        return dt.ToString("MMM-dd HH:mm");
//    }
//}
