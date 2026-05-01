using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Budgets;
using Points.Views.Shared;

namespace Points.Views.Budgets;

public partial class BudgetTransactionLogPage : ContentPage
{
    public BudgetTransactionLogPage(
        List<BudgetTransaction> transactions,
        TaskCompletionSource<List<BudgetTransaction>> tcs,
        double exchangeRate,
        IUdmdService udmd,
        ITimeZoneService timeZoneService,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        if (tcs is null) throw new ArgumentNullException(nameof(tcs));
        if (udmd is null) throw new ArgumentNullException(nameof(udmd));
        if (transactions is null) throw new ArgumentNullException(nameof(transactions));

        BindingContext = new BudgetTransactionLogViewModel(
            transactions: transactions,

            onSave: edited =>
            {
                tcs.TrySetResult(edited);
                _ = navigation.PopAsync();
            },

            pickDateTime: async row =>
            {
                if (row is null) return null;

                return await DateTimePickerSheet.PickAsync(
                    page: this,
                    navigation: navigation,
                    initial: row.Timestamp,
                    min: DateTime.MinValue,
                    max: DateTime.MaxValue,
                    validateAsync: null,
                    title: "Edit timestamp");
            },
            udmd: udmd,
            timeZoneService: timeZoneService,
            navigation: navigation,
            dialogs: dialogs,
            exchangeRate: exchangeRate
        );
    }
}
