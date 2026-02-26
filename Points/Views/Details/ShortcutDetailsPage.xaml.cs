using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class ShortcutDetailsPage : ContentPage
{
    private readonly List<string> _existingGroupNames;

    public ShortcutDetailsPage(
        ShortcutModel model,
        Dictionary<TargetCardType, List<CardOption>> optionsByType,
        List<string> existingGroupNames,
        Action<ShortcutModel> onSaved,
        Action<ShortcutModel>? onDelete = null,
        TargetCardType defaultType = TargetCardType.MainQuest)
    {
        InitializeComponent();

        var vm = new ShortcutDetailsViewModel(
            model: model,
            onSaved: onSaved,
            onDelete: onDelete,
            optionsByType: optionsByType,
            defaultType: defaultType);

        BindingContext = vm;

        _existingGroupNames = existingGroupNames ?? new();
    }

    private async void OnEditGroupClicked(object sender, EventArgs e)
    {
        if (BindingContext is not ShortcutDetailsViewModel vm)
            return;

        var initial = string.IsNullOrWhiteSpace(vm.GroupName)
            ? Array.Empty<string>()
            : new[] { vm.GroupName.Trim() };

        var page = new MultiSelectPickerPage(
            title: "Select Group",
            items: _existingGroupNames,
            initial: initial,
            isReadOnly: false,
            isSingleTag: true);

        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result;
        if (result == null)
            return;

        vm.GroupName = result.FirstOrDefault()?.Trim() ?? "";
    }

    private void OnClearGroupClicked(object sender, EventArgs e)
    {
        if (BindingContext is ShortcutDetailsViewModel vm)
            vm.GroupName = "";
    }
}