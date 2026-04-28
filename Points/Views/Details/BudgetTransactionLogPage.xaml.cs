using Points.Models;
using Points.Helpers;
using Points.Services;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;
using Points.Views.Shared;

namespace Points.Views.Details;

public partial class BudgetTransactionLogPage : ContentPage
{
    private readonly TaskCompletionSource<List<BudgetTransaction>> _tcs;
    private readonly IDbService _db;
    private readonly ITimeZoneService _timeZoneService;

    public BudgetTransactionLogPage(List<BudgetTransaction> transactions, TaskCompletionSource<List<BudgetTransaction>> tcs, double exchangeRate, IDbService db, ITimeZoneService? timeZoneService = null)
    {
        InitializeComponent();

        _tcs = tcs ?? throw new ArgumentNullException(nameof(tcs));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeZoneService = timeZoneService ?? ServiceHelper.GetService<ITimeZoneService>();
        if (transactions is null) throw new ArgumentNullException(nameof(transactions));

        var localTransactions = transactions
            .Select(ToEditorLocalTransaction)
            .ToList();

        BindingContext = new BudgetTransactionLogViewModel(
            transactions: localTransactions,

            onSave: edited =>
            {
                _tcs.TrySetResult(edited.Select(ToUtcTransaction).ToList());
                _ = Navigation.PopAsync();
            },

            pickDateTime: CreatePickDateTimeDelegate(),

            confirmDelete: (title, message) =>
            {
                return DisplayAlert(title, message, "Delete", "Cancel");
            },

            pickType: async current =>
            {
                var choice = await DisplayActionSheet("Transaction type", "Cancel", null, "Spend", "CashIn");
                return choice switch
                {
                    "Spend" => BudgetTransactionType.Spend,
                    "CashIn" => BudgetTransactionType.CashIn,
                    _ => (BudgetTransactionType?)null
                };
            },

            promptAmount: async current =>
            {
                // Returns string so VM can parse/validate
                return await DisplayPromptAsync(
                    title: "Amount",
                    message: "Enter amount",
                    accept: "OK",
                    cancel: "Cancel",
                    placeholder: "e.g. 120",
                    initialValue: current);
            },

            exchangeRate: exchangeRate
        );

        _ = LoadMetadataSummariesAsync();
    }

    private BudgetTransaction ToEditorLocalTransaction(BudgetTransaction transaction)
    {
        return new BudgetTransaction
        {
            Id = transaction.Id,
            Timestamp = TimeDisplayFormatter.ToLocalInstant(transaction.Timestamp, _timeZoneService),
            Type = transaction.Type,
            CurrencyAmount = transaction.CurrencyAmount,
            GlobalValueAmount = transaction.GlobalValueAmount
        };
    }

    private BudgetTransaction ToUtcTransaction(BudgetTransaction transaction)
    {
        return new BudgetTransaction
        {
            Id = transaction.Id,
            Timestamp = transaction.Timestamp.Kind == DateTimeKind.Utc
                ? transaction.Timestamp
                : _timeZoneService.ToUtcFromLocal(transaction.Timestamp),
            Type = transaction.Type,
            CurrencyAmount = transaction.CurrencyAmount,
            GlobalValueAmount = transaction.GlobalValueAmount
        };
    }

    private async Task LoadMetadataSummariesAsync()
    {
        if (BindingContext is not BudgetTransactionLogViewModel vm)
            return;

        foreach (var row in vm.Rows.Where(x => x.Id > 0))
        {
            var metadata = await _db.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.BudgetTransaction, row.Id);
            if (metadata.Count == 0)
                continue;

            row.MetadataSummary = string.Join(Environment.NewLine, metadata.Select(x =>
                $"{x.FieldName}: {UdmdValueFormatter.ToDisplayString(x)}"));
        }
    }

    private async void OnMetadataClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: BudgetTransactionRow row })
            return;

        var metadata = await _db.GetMetadataForEntityAsync(UdmdRelatedEntityTypes.BudgetTransaction, row.Id);
        if (metadata.Count == 0)
            return;

        await Navigation.PushModalAsync(CreateMetadataViewerPage(metadata));
    }

    private ContentPage CreateMetadataViewerPage(IReadOnlyList<UdmdTransModel> metadata)
    {
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };

        foreach (var item in metadata)
        {
            stack.Children.Add(CreateMetadataRow(item));
        }

        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Colors.Gray,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            CornerRadius = 12
        };

        var page = new ContentPage
        {
            Title = "Metadata",
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto }
                },
                Children =
                {
                    new ScrollView { Content = stack },
                    closeButton
                }
            }
        };

        Grid.SetRow(closeButton, 1);
        closeButton.Clicked += async (_, __) => await page.Navigation.PopModalAsync();

        return page;
    }

    private View CreateMetadataRow(UdmdTransModel item)
    {
        var fieldLabel = new Label
        {
            Text = item.FieldName,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };

        if (item.FieldTypeKind == UdmdFieldType.Image)
        {
            var fileNameLabel = new Label
            {
                Text = item.FieldValue,
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center
            };

            var viewButton = new Button
            {
                Text = "View",
                BackgroundColor = Colors.DodgerBlue,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 10,
                WidthRequest = 90,
                HeightRequest = 40,
                IsEnabled = UdmdImageFileStore.ImageExists(item.CardID, item.FieldValue)
            };

            if (!viewButton.IsEnabled)
                viewButton.BackgroundColor = Colors.Gray;

            viewButton.Clicked += async (_, __) => await OpenImageMetadataAsync(item);

            var imageRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 10,
                Children = { fileNameLabel, viewButton }
            };
            Grid.SetColumn(viewButton, 1);

            return new VerticalStackLayout
            {
                Spacing = 4,
                Children = { fieldLabel, imageRow }
            };
        }

        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                fieldLabel,
                new Label
                {
                    Text = UdmdValueFormatter.ToDisplayString(item),
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };
    }

    private async Task OpenImageMetadataAsync(UdmdTransModel item)
    {
        if (!UdmdImageFileStore.ImageExists(item.CardID, item.FieldValue))
        {
            await DisplayAlert("Image not found", "The stored metadata image could not be found.", "OK");
            return;
        }

        var path = UdmdImageFileStore.GetImagePath(item.CardID, item.FieldValue);
        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Colors.Gray,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            CornerRadius = 12
        };

        var page = new ContentPage
        {
            Title = item.FieldName,
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto }
                },
                Padding = 12,
                Children =
                {
                    new Image
                    {
                        Source = ImageSource.FromFile(path),
                        Aspect = Aspect.AspectFit
                    },
                    closeButton
                }
            }
        };

        Grid.SetRow(closeButton, 1);
        closeButton.Clicked += async (_, __) => await page.Navigation.PopModalAsync();

        await Navigation.PushModalAsync(page);
    }

    private Func<BudgetTransactionRow, Task<DateTime?>> CreatePickDateTimeDelegate()
    {
        return async row =>
        {
            if (row is null) return null;

            // Reuse the existing DateTimePickerSheet helper for transaction timestamp edits.
            return await DateTimePickerSheet.PickAsync(
                page: this,
                initial: row.Timestamp,
                min: DateTime.MinValue,
                max: DateTime.MaxValue,
                validateAsync: null,
                title: "Edit timestamp");
        };
    }
}
