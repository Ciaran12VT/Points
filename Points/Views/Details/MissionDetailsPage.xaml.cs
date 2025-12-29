using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class MissionDetailsPage : ContentPage
{
    private readonly List<string> _allTags;

    public MissionDetailsPage(
        MissionCardModel model,
        Action<MissionCardModel> onSaved,
        Action<MissionCardModel> onDelete,
        Action<MissionCardModel> onFail,
        List<string> availableTagsList)
    {
        InitializeComponent();
        BindingContext = new MissionDetailsViewModel(model, onSaved, onDelete, onFail, availableTagsList);
        _allTags = availableTagsList;
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
}