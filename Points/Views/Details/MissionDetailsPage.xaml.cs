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
}