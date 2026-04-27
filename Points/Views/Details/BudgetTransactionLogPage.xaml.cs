using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;
using Points.Views.Shared;

namespace Points.Views.Details;

public partial class BudgetTransactionLogPage : ContentPage
{
    private readonly TaskCompletionSource<List<BudgetTransaction>> _tcs;
    private readonly IDbService _db;

    public BudgetTransactionLogPage(List<BudgetTransaction> transactions, TaskCompletionSource<List<BudgetTransaction>> tcs, double exchangeRate, IDbService db)
    {
        InitializeComponent();

        _tcs = tcs ?? throw new ArgumentNullException(nameof(tcs));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        if (transactions is null) throw new ArgumentNullException(nameof(transactions));

        BindingContext = new BudgetTransactionLogViewModel(
            transactions: transactions,

            onSave: edited =>
            {
                _tcs.TrySetResult(edited);
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

        if (string.IsNullOrWhiteSpace(row.MetadataSummary))
            return;

        await DisplayAlert("Metadata", row.MetadataSummary, "OK");
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
