using Points.Models;
using Points.Services.Backup;
using System.Collections.ObjectModel;

namespace Points.Views.Settings;

public partial class BackupSelectionPage : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _selectionCompletion = new();

    public BackupSelectionPage(
        string pageTitle,
        string message,
        string confirmText,
        IEnumerable<BackupResourceOption> options)
    {
        InitializeComponent();

        PageTitle = pageTitle;
        Message = message;
        ConfirmText = confirmText;
        Items = new ObservableCollection<BackupSelectionItem>(
            options.Select(option => new BackupSelectionItem(option)));

        BindingContext = this;
    }

    public string PageTitle { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public ObservableCollection<BackupSelectionItem> Items { get; }
    public Task<IReadOnlyList<string>?> SelectionTask => _selectionCompletion.Task;

    protected override bool OnBackButtonPressed()
    {
        _selectionCompletion.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _selectionCompletion.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var selectedKeys = Items
            .Where(x => x.IsSelected)
            .Select(x => x.Key)
            .ToList();

        if (selectedKeys.Count == 0)
        {
            await DisplayAlert(PageTitle, "Select at least one item.", "OK");
            return;
        }

        _selectionCompletion.TrySetResult(selectedKeys);
        await Navigation.PopModalAsync();
    }
}
