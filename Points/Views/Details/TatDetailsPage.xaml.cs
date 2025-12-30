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

}