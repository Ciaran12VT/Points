using Points.Models;
using Points.ViewModels;
using System.Diagnostics;

namespace Points.Views.Details;

public partial class TatDetailsPage : ContentPage
{
    private readonly TatCardModel _model;
    private readonly List<string> _allTags;

    public TatDetailsPage(TatCardModel model, Action<TatCardModel> onSaved, Action<TatCardModel> onDelete, List<string> availableTagsList)
    {
        InitializeComponent();
        BindingContext = new TatDetailsViewModel(model, onSaved, onDelete, availableTagsList);
        _model = model;
        _allTags = availableTagsList;
    }

    private async void OnEditTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not TatDetailsViewModel vm)
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
        if (BindingContext is TatDetailsViewModel vm)
            vm.Tags = "";
    }

    private async void OnEditActiveTimeClicked(object sender, EventArgs e)
    {
        var tcs = new TaskCompletionSource<List<Tuple<DateTime, DateTime>>>();

        var page = new Points.Views.Details.EditActiveTimePage(_model.Activity, tcs);
        await Navigation.PushAsync(page);

        try
        {
            var edited = await tcs.Task;   // user hit Save
            _model.Activity = edited;      // store it wherever you keep it
        }
        catch (TaskCanceledException)
        {
            // user backed out, ignore
        }
    }

    private async void OnSetActiveTimeTargetClicked(object sender, EventArgs e)
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
        if (BindingContext is TatDetailsViewModel typedVm)
        {
            var totalHours = (int)result.Value.TotalHours;
            var formatted = $"{totalHours}:{result.Value.Minutes:D2}:{result.Value.Seconds:D2}";

            //Hold the target in a property in the VM
            typedVm.TargetActiveTime = result;
            typedVm.RaisePropertyChanged(nameof(typedVm.HasTargetActiveTime));
            typedVm.RaisePropertyChanged(nameof(typedVm.ActiveTimeTargetLabelColor));
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is TatDetailsViewModel vm)
            vm.StopTimer();
    }

}