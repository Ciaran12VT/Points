using Points.Models;
using Points.Helpers;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class MissionDetailsPage : ContentPage
{
    private readonly List<string> _allTags;
    private readonly MissionCardModel _model;
    private readonly IActivityService _activity;
    private readonly IUdmdService _udmd;

    public MissionDetailsPage(
        MissionCardModel model,
        Action<MissionCardModel> onSaved,
        Action<MissionCardModel> onDelete,
        Action<MissionCardModel> onFail,
        List<string> availableTagsList,
        IActivityService activity,
        IUdmdService udmd)
    {
        InitializeComponent();
        BindingContext = new MissionDetailsViewModel(
            model,
            onSaved,
            onDelete,
            onFail,
            availableTagsList,
            ServiceHelper.GetService<ITimeZoneService>());
        _allTags = availableTagsList;
        _model = model;
        _activity = activity;
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await TryFocusTitleIfEmptyAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TryFocusTitleIfEmptyAsync();
    }

    private async Task TryFocusTitleIfEmptyAsync()
    {
        if (TitleEntry == null)
            return;

        // If text is not empty, do nothing.
        if (!string.IsNullOrWhiteSpace(TitleEntry.Text))
            return;

        // If it can't be focused, do nothing.
        if (!TitleEntry.IsEnabled || TitleEntry.IsReadOnly || !TitleEntry.IsVisible)
            return;

        // Let navigation + layout settle
        await Task.Delay(50);

        // Focus can still fail; retry a couple of times
        for (int i = 0; i < 3; i++)
        {
            MainThread.BeginInvokeOnMainThread(() => TitleEntry.Focus());
            await Task.Delay(50);

            if (TitleEntry.IsFocused)
                return;
        }
    }

    private void OnAvailableFromDateSelected(object sender, DateChangedEventArgs e)
    {
        // 224 hours = 9 days + 8 hours
        const int bumpHours = 224;

        // Use dynamic so we don't need the exact VM type name here.
        // Assumes your BindingContext has:
        // AvailableFromDate (DateTime), AvailableFromTime (TimeSpan),
        // DueDate (DateTime), DueTime (TimeSpan)
        if (BindingContext is not object ctx) return;
        dynamic vm = ctx;

        try
        {
            DateTime available = ((DateTime)vm.AvailableFromDate).Date
                                 + (TimeSpan)vm.AvailableFromTime;

            DateTime due = ((DateTime)vm.DueDate).Date
                           + (TimeSpan)vm.DueTime;

            if (available > due)
            {
                var newDue = available.AddHours(bumpHours);

                // Keep your split Date + Time bindings consistent
                vm.DueDate = newDue.Date;
                vm.DueTime = newDue.TimeOfDay;
            }
        }
        catch
        {
            // If the VM doesn't expose those properties, nothing happens.
            // (Remove this catch if you prefer to fail loudly during dev.)
        }
    }


    private async void OnEditTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not MissionDetailsViewModel vm)
            return;

        var initial = (vm.Tags ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var page = new MultiSelectPickerPage(
            "Select Tags",
            _allTags,
            initial,
            false
        );

        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result;
        if (result == null)
            return; // cancelled

        vm.Tags = string.Join(", ", result);
    }

    private void OnClearTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is MissionDetailsViewModel vm)
            vm.Tags = "";
    }


    private async void OnEditEstimatedTimeClicked(object sender, EventArgs e)
    {
        // 1) Start values (parse from the current display if you want)
        // If you already store a TimeSpan on the VM, use that instead.
        var vm = BindingContext; // cast to your AchievementDetailsViewModel if you want

        // 2) Push your picker page
        // This assumes your DurationPickerPage returns a TimeSpan (or null if cancelled).
        var page = new DurationPickerPage(
        /* pass current duration here if your ctor needs it */
        );

        // OPTION A: if DurationPickerPage exposes a TaskCompletionSource result
        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result; // e.g. Task<TimeSpan?>
        if (result is null) return;

        // 3) Write back to VM
        // Replace with your real VM property
        if (BindingContext is MissionDetailsViewModel typedVm)
        {
            var totalHours = (int)result.Value.TotalHours;
            var formatted = $"{totalHours}:{result.Value.Minutes:D2}:{result.Value.Seconds:D2}";

            typedVm.EstimatedTimeTs = result.Value;          // if you store TimeSpan
            typedVm.EstimatedTimeText = formatted; // if you store string
        }
    }

    private async void OnEditActiveTimeClicked(object sender, EventArgs e)
    {
        var tcs = new TaskCompletionSource<List<ActivityModel>>();

        var page = new Points.Views.Details.EditActiveTimePage(_model.Activity, tcs, _activity, _udmd);

        await Navigation.PushAsync(page);

        try
        {
            var edited = await tcs.Task;   // user hit Save

            if (_model.CardID > 0)
            {
                var result = await _activity.UpsertActivitiesAsync(edited, _model.CardID);
                if (!result.Success)
                {
                    await DisplayAlert("Active time not saved", result.Message, "OK");
                    return;
                }
            }

            _model.Activity = edited;
        }
        catch (TaskCanceledException)
        {
            // user backed out, ignore
        }
    }

    private async void OnAddResourceImagesClicked(object sender, EventArgs e)
    {
        if (BindingContext is not MissionDetailsViewModel vm) return;

        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Pick images (resources)",
                FileTypes = FilePickerFileType.Images
            });

            foreach (var r in results ?? Enumerable.Empty<FileResult>())
            {
                if (!string.IsNullOrWhiteSpace(r.FullPath))
                    vm.ResourcesToAdd.Add(r.FullPath);
            }
        }
        catch (TaskCanceledException) { }
    }

    private async void OnAddResourceFilesClicked(object sender, EventArgs e)
    {
        if (BindingContext is not MissionDetailsViewModel vm) return;

        try
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Pick files (resources)"
                // no FileTypes => any
            });

            foreach (var r in results ?? Enumerable.Empty<FileResult>())
            {
                if (!string.IsNullOrWhiteSpace(r.FullPath))
                    vm.ResourcesToAdd.Add(r.FullPath);
            }
        }
        catch (TaskCanceledException) { }
    }



    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is MissionDetailsViewModel vm)
            vm.StopTimer();
    }
}
