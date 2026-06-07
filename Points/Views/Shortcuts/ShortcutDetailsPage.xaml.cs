using Points.Models;
using Points.Services.Navigation;
using Points.ViewModels.Shortcuts;

namespace Points.Views.Shortcuts;

public partial class ShortcutDetailsPage : ContentPage
{
    public ShortcutDetailsPage(
        ShortcutModel model,
        Dictionary<TargetCardType, List<CardOption>> optionsByType,
        List<ShortcutGroupModel> existingGroups,
        Action<ShortcutModel> onSaved,
        Action<ShortcutModel>? onDelete = null,
        TargetCardType defaultType = TargetCardType.MainQuest,
        IAppNavigationService? navigation = null,
        IAppDialogService? dialogs = null)
    {
        InitializeComponent();

        var appNavigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        var appDialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        BindingContext = new ShortcutDetailsViewModel(
            model: model,
            onSaved: onSaved,
            onDelete: onDelete,
            optionsByType: optionsByType,
            existingGroups: existingGroups,
            navigation: appNavigation,
            dialogs: appDialogs,
            defaultType: defaultType);
    }
}
