using System.Collections.ObjectModel;
using Points.Models;
using Points.Services;
using Points.Services.Navigation;
using Points.Views.Budgets;

namespace Points.ViewModels.Budgets;

public sealed class BudgetTransactionMetadataRow
{
    public BudgetTransactionMetadataRow(UdmdTransModel metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        FieldName = metadata.FieldName;
        CardId = metadata.CardID;
        StoredFileName = metadata.FieldValue;
        IsImage = metadata.FieldTypeKind == UdmdFieldType.Image;
        DisplayValue = IsImage
            ? metadata.FieldValue
            : UdmdValueFormatter.ToDisplayString(metadata);
    }

    public string FieldName { get; }

    public string DisplayValue { get; }

    public bool IsImage { get; }

    public long CardId { get; }

    public string StoredFileName { get; }

    public bool CanViewImage => IsImage && UdmdImageFileStore.ImageExists(CardId, StoredFileName);
}

public sealed class BudgetTransactionMetadataViewerViewModel : BindableObject
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

    public ObservableCollection<BudgetTransactionMetadataRow> Rows { get; } = new();

    public Command CloseCommand { get; }

    public Command<BudgetTransactionMetadataRow> OpenImageCommand { get; }

    public BudgetTransactionMetadataViewerViewModel(
        IEnumerable<UdmdTransModel> metadata,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        foreach (var item in metadata)
            Rows.Add(new BudgetTransactionMetadataRow(item));

        CloseCommand = new Command(async () => await _navigation.PopModalAsync());
        OpenImageCommand = new Command<BudgetTransactionMetadataRow>(async row => await OpenImageAsync(row));
    }

    private async Task OpenImageAsync(BudgetTransactionMetadataRow? row)
    {
        if (row is null || !row.IsImage)
            return;

        if (!UdmdImageFileStore.ImageExists(row.CardId, row.StoredFileName))
        {
            await _dialogs.DisplayAlertAsync(
                "Image not found",
                "The stored metadata image could not be found.",
                "OK");
            return;
        }

        var path = UdmdImageFileStore.GetImagePath(row.CardId, row.StoredFileName);
        await _navigation.PushModalAsync(
            new BudgetTransactionMetadataImagePage(
                new BudgetTransactionMetadataImageViewModel(row.FieldName, path, _navigation)));
    }
}

public sealed class BudgetTransactionMetadataImageViewModel
{
    private readonly IAppNavigationService _navigation;

    public BudgetTransactionMetadataImageViewModel(
        string title,
        string imagePath,
        IAppNavigationService navigation)
    {
        Title = title;
        ImagePath = imagePath;
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        CloseCommand = new Command(async () => await _navigation.PopModalAsync());
    }

    public string Title { get; }

    public string ImagePath { get; }

    public Command CloseCommand { get; }
}
