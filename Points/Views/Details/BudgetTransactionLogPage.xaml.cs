using Points.Models;
using Points.ViewModels;
using Points.Views.Shared;

namespace Points.Views.Details;

public partial class BudgetTransactionLogPage : ContentPage
{
    private readonly TaskCompletionSource<List<BudgetTransaction>> _tcs;

    public BudgetTransactionLogPage(List<BudgetTransaction> transactions, TaskCompletionSource<List<BudgetTransaction>> tcs, double exchangeRate)
    {
        InitializeComponent();

        _tcs = tcs ?? throw new ArgumentNullException(nameof(tcs));
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
    }

    private Func<BudgetTransactionRow, Task<DateTime?>> CreatePickDateTimeDelegate()
    {
        return async row =>
        {
            if (row is null) return null;

            // Reuse your existing DateTimePickerSheet (it’s internal; keep it in same namespace/file as before or make it public)
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
