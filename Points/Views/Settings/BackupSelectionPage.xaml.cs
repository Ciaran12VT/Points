using Points.Models;
using Points.Services.Backup;
using Points.Services.Navigation;
using System.Collections.ObjectModel;

namespace Points.Views.Settings;

public partial class BackupSelectionPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _selectionCompletion = new();

    public BackupSelectionPage(
        string pageTitle,
        string message,
        string confirmText,
        IEnumerable<BackupResourceOption> options,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IEnumerable<string>? selectedKeys = null)
    {
        InitializeComponent();

        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        CancelCommand = new Command(async () => await CancelAsync());
        ConfirmCommand = new Command(async () => await ConfirmAsync());
        PageTitle = pageTitle;
        Message = message;
        ConfirmText = confirmText;
        var selectedKeySet = selectedKeys?.ToHashSet(StringComparer.Ordinal);
        Items = new ObservableCollection<BackupSelectionItem>(
            options.Select(option => new BackupSelectionItem(
                option,
                selectedKeySet == null || selectedKeySet.Contains(option.Key))));

        BindingContext = this;
    }

    public string PageTitle { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public ObservableCollection<BackupSelectionItem> Items { get; }
    public Task<IReadOnlyList<string>?> SelectionTask => _selectionCompletion.Task;
    public Command CancelCommand { get; }
    public Command ConfirmCommand { get; }

    protected override bool OnBackButtonPressed()
    {
        _selectionCompletion.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    private async Task CancelAsync()
    {
        _selectionCompletion.TrySetResult(null);
        await _navigation.PopModalAsync();
    }

    private async Task ConfirmAsync()
    {
        var selectedKeys = Items
            .Where(x => x.IsSelected)
            .Select(x => x.Key)
            .ToList();

        if (selectedKeys.Count == 0)
        {
            await _dialogs.DisplayAlertAsync(PageTitle, "Select at least one item.", "OK");
            return;
        }

        _selectionCompletion.TrySetResult(selectedKeys);
        await _navigation.PopModalAsync();
    }
}
