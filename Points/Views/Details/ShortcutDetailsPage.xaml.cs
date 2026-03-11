using Points.Models;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class ShortcutDetailsPage : ContentPage
{
    private readonly List<string> _existingGroupNames;
    private readonly List<ShortcutGroupModel> _existingGroups;

    public ShortcutDetailsPage(
        ShortcutModel model,
        Dictionary<TargetCardType, List<CardOption>> optionsByType,
        List<ShortcutGroupModel> existingGroups,
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

        _existingGroups = existingGroups ?? new List<ShortcutGroupModel>();
        _existingGroupNames = _existingGroups?.Select(x => x.Name).ToList() ?? new List<string>();
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

        vm.SelectedGroup = _existingGroups.Find(g => g.Name.Equals(result.FirstOrDefault()?.Trim(), StringComparison.OrdinalIgnoreCase));
        vm.GroupName = result.FirstOrDefault()?.Trim() ?? "";
    }

    private void OnClearGroupClicked(object sender, EventArgs e)
    {
        if (BindingContext is ShortcutDetailsViewModel vm)
            vm.GroupName = "";
    }
}