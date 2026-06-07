using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Views.Budgets;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Points.ViewModels.Budgets;

public sealed class BudgetTransactionRow : BindableObject
{
    public int Id { get; }

    private DateTime _timestamp;
    private BudgetTransactionType _type;
    private double _amount;
    private string _metadataSummary = "";

    public DateTime Timestamp
    {
        get => _timestamp;
        private set
        {
            if (_timestamp == value) return;
            _timestamp = value;
            OnPropertyChanged(nameof(Timestamp));
            OnPropertyChanged(nameof(TimestampText));
        }
    }

    public BudgetTransactionType Type
    {
        get => _type;
        private set
        {
            if (_type == value) return;
            _type = value;
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(TypeText));
        }
    }

    public double Amount
    {
        get => _amount;
        private set
        {
            if (Math.Abs(_amount - value) < 0.0000001) return;
            _amount = value;
            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(AmountText));
        }
    }

    public string AmountText => Amount.ToString("0.##", CultureInfo.InvariantCulture);

    public string TypeText => Type == BudgetTransactionType.Spend ? "Spend" : "CashIn";

    public string TimestampText => TimeDisplayFormatter.FormatLocal(Timestamp, "MMM-dd HH:mm");

    public string MetadataSummary
    {
        get => _metadataSummary;
        set
        {
            if (_metadataSummary == value) return;
            _metadataSummary = value;
            OnPropertyChanged(nameof(MetadataSummary));
            OnPropertyChanged(nameof(HasMetadata));
        }
    }

    public bool HasMetadata => !string.IsNullOrWhiteSpace(MetadataSummary);

    public BudgetTransactionRow(BudgetTransaction model)
    {
        Id = model.Id;
        _timestamp = model.Timestamp;
        _type = model.Type;
        _amount = model.CurrencyAmount;
    }

    public void SetTimestamp(DateTime dt) => Timestamp = dt;

    public void SetType(BudgetTransactionType type) => Type = type;

    public bool TrySetAmountFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return false;

        if (v < 0) return false;

        Amount = v;
        return true;
    }

    public BudgetTransaction ToModel(double exchangeRate)
    {
        // Recompute GlobalValueAmount based on current ExchangeRate (consistent with your AddCashIn)
        // If you later want historical rate preservation, store rate-per-txn instead.
        var global = Type == BudgetTransactionType.CashIn
            ? Amount * exchangeRate
            : 0;

        return new BudgetTransaction
        {
            Id = Id,
            Timestamp = Timestamp,
            Type = Type,
            CurrencyAmount = Amount,
            GlobalValueAmount = global
        };
    }
}

public sealed class BudgetTransactionLogViewModel : BindableObject
{
    private readonly Action<List<BudgetTransaction>> _onSave;
    private readonly Func<BudgetTransactionRow, Task<DateTime?>> _pickDateTime;
    private readonly IUdmdService _udmd;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

    // Needed to recompute GlobalValueAmount for CashIn on Save.
    // We can’t pull this from the transaction model itself reliably (it may be stale after edits).
    private readonly double _exchangeRate;

    public ObservableCollection<BudgetTransactionRow> Rows { get; } = new();

    public Command<BudgetTransactionRow> EditAmountCommand { get; }
    public Command<BudgetTransactionRow> EditTypeCommand { get; }
    public Command<BudgetTransactionRow> EditTimestampCommand { get; }
    public Command<BudgetTransactionRow> OpenMetadataCommand { get; }
    public Command<BudgetTransactionRow> DeleteRowCommand { get; }
    public Command SaveCommand { get; }

    public BudgetTransactionLogViewModel(
        List<BudgetTransaction> transactions,
        Action<List<BudgetTransaction>> onSave,
        Func<BudgetTransactionRow, Task<DateTime?>> pickDateTime,
        IUdmdService udmd,
        ITimeZoneService timeZoneService,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        double exchangeRate = 0.01)
    {
        _onSave = onSave ?? throw new ArgumentNullException(nameof(onSave));
        _pickDateTime = pickDateTime ?? throw new ArgumentNullException(nameof(pickDateTime));
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        _exchangeRate = exchangeRate;

        if (transactions is null) throw new ArgumentNullException(nameof(transactions));

        foreach (var t in transactions.Select(ToEditorLocalTransaction).OrderByDescending(x => x.Timestamp))
            Rows.Add(new BudgetTransactionRow(t));

        EditAmountCommand = new Command<BudgetTransactionRow>(async row =>
        {
            if (row is null) return;

            var input = await PromptAmountAsync(row.AmountText);
            if (input is null) return;

            if (!row.TrySetAmountFromText(input))
                return; // (optional) show error; keeping minimal for parity with your pattern
        });

        EditTypeCommand = new Command<BudgetTransactionRow>(async row =>
        {
            if (row is null) return;

            var chosen = await PickTypeAsync();
            if (chosen is null) return;

            row.SetType(chosen.Value);
        });

        EditTimestampCommand = new Command<BudgetTransactionRow>(async row =>
        {
            if (row is null) return;

            var chosen = await _pickDateTime(row);
            if (chosen is null) return;

            row.SetTimestamp(chosen.Value);
            Resort();
        });

        OpenMetadataCommand = new Command<BudgetTransactionRow>(async row => await OpenMetadataAsync(row));

        DeleteRowCommand = new Command<BudgetTransactionRow>(async row =>
        {
            if (row is null) return;

            var confirm = await ConfirmDeleteAsync(row);

            if (!confirm) return;

            Rows.Remove(row);
        });

        SaveCommand = new Command(() =>
        {
            var edited = Rows
                .OrderByDescending(r => r.Timestamp)
                .Select(r => ToUtcTransaction(r.ToModel(_exchangeRate)))
                .ToList();

            _onSave(edited);
        });

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
        foreach (var row in Rows.Where(x => x.Id > 0))
        {
            var metadata = await _udmd.GetMetadataForEntityAsync(
                UdmdRelatedEntityTypes.BudgetTransaction,
                row.Id);

            if (metadata.Count == 0)
                continue;

            row.MetadataSummary = string.Join(Environment.NewLine, metadata.Select(x =>
                $"{x.FieldName}: {UdmdValueFormatter.ToDisplayString(x)}"));
        }
    }

    private async Task OpenMetadataAsync(BudgetTransactionRow? row)
    {
        if (row is null || row.Id <= 0)
            return;

        var metadata = await _udmd.GetMetadataForEntityAsync(
            UdmdRelatedEntityTypes.BudgetTransaction,
            row.Id);

        if (metadata.Count == 0)
            return;

        await _navigation.PushModalAsync(
            new BudgetTransactionMetadataViewerPage(
                metadata,
                _navigation,
                _dialogs));
    }

    private Task<bool> ConfirmDeleteAsync(BudgetTransactionRow row)
    {
        return _dialogs.DisplayAlertAsync(
            "Delete transaction?",
            $"{row.TypeText} {row.AmountText} @ {row.TimestampText}",
            "Delete",
            "Cancel");
    }

    private async Task<BudgetTransactionType?> PickTypeAsync()
    {
        var choice = await _dialogs.DisplayActionSheetAsync(
            "Transaction type",
            "Cancel",
            null,
            "Spend",
            "CashIn");

        return choice switch
        {
            "Spend" => BudgetTransactionType.Spend,
            "CashIn" => BudgetTransactionType.CashIn,
            _ => null
        };
    }

    private Task<string?> PromptAmountAsync(string current)
    {
        return _dialogs.DisplayPromptAsync(
            title: "Amount",
            message: "Enter amount",
            accept: "OK",
            cancel: "Cancel",
            placeholder: "e.g. 120",
            initialValue: current);
    }

    private void Resort()
    {
        var sorted = Rows.OrderByDescending(r => r.Timestamp).ToList();
        Rows.Clear();
        foreach (var r in sorted) Rows.Add(r);
    }
}
