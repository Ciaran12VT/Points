using Points.Models;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using System.Collections.ObjectModel;

namespace Points.Services.Budgets;

public sealed class SqliteBudgetService : IBudgetService
{
    private readonly ISqliteConnectionContext _context;
    private readonly ITimeZoneService _timeZoneService;

    public SqliteBudgetService(ISqliteConnectionContext context, ITimeZoneService timeZoneService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
    }

    public async Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id)
    {
        await _context.InitializeAsync();

        const string sql = @"
            SELECT
                b.BudgetCardID     AS BudgetCardID,
                b.CardID           AS CardID,
                c.Title            AS Title,
                c.Tags             AS Tags,
                b.Status           AS Status,
                b.Description      AS Description,
                b.Currency         AS Currency,
                b.ExchangeRate     AS ExchangeRate,
                b.StartDate        AS StartDate,
                b.InitialBalance   AS InitialBalance
            FROM BudgetCard b
            JOIN Card c ON c.CardID = b.CardID
            WHERE b.BudgetCardID = ?
            LIMIT 1;";

        var row = (await _context.Db.QueryAsync<BudgetCardJoinedRow>(sql, id)).FirstOrDefault();
        if (row == null)
            throw new KeyNotFoundException($"BudgetCard not found. BudgetCardID={id}");

        var model = MapBudgetRowToModel(row);
        await LoadTopUpsAsync(model);
        await LoadTransactionsAsync(model);

        return model;
    }

    public async Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string? whereClause = null)
    {
        await _context.InitializeAsync();

        var sql = @"
            SELECT
                b.BudgetCardID     AS BudgetCardID,
                b.CardID           AS CardID,
                c.Title            AS Title,
                c.Tags             AS Tags,
                b.Status           AS Status,
                b.Description      AS Description,
                b.Currency         AS Currency,
                b.ExchangeRate     AS ExchangeRate,
                b.StartDate        AS StartDate,
                b.InitialBalance   AS InitialBalance
            FROM BudgetCard b
            JOIN Card c ON c.CardID = b.CardID";

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            var wc = whereClause.Trim();

            if (wc.StartsWith("WHERE", StringComparison.OrdinalIgnoreCase) ||
                wc.StartsWith("ORDER BY", StringComparison.OrdinalIgnoreCase) ||
                wc.StartsWith("LIMIT", StringComparison.OrdinalIgnoreCase))
            {
                sql += " " + wc;
            }
            else
            {
                sql += " WHERE " + wc;
            }
        }

        var rows = await _context.Db.QueryAsync<BudgetCardJoinedRow>(sql);
        if (rows.Count == 0)
            return new List<BudgetCardModel>();

        var models = rows.Select(MapBudgetRowToModel).ToList();
        var byBudgetId = models.ToDictionary(m => m.Id);
        var budgetIds = rows.Select(r => r.BudgetCardID).Distinct().ToList();

        if (budgetIds.Count == 0)
            return models;

        var placeholders = string.Join(", ", budgetIds.Select(_ => "?"));

        var topupsSql = $@"
            SELECT
                BudgetCardScheduledTopUpID AS BudgetCardScheduledTopUpID,
                BudgetCardID               AS BudgetCardID,
                Amount                     AS Amount,
                TimeOfDaySeconds           AS TimeOfDaySeconds
            FROM BudgetCardScheduledTopUp
            WHERE BudgetCardID IN ({placeholders})
            ORDER BY BudgetCardID, BudgetCardScheduledTopUpID;";

        var topupRows = await _context.Db.QueryAsync<BudgetTopUpRow>(topupsSql, budgetIds.Cast<object>().ToArray());

        foreach (var topup in topupRows)
        {
            if (!byBudgetId.TryGetValue(topup.BudgetCardID, out var parent))
                continue;

            parent.TopUps.Add(MapTopUpRowToModel(topup));
        }

        var transSql = $@"
            SELECT
                BudgetCardTransactionID AS BudgetCardTransactionID,
                BudgetCardID            AS BudgetCardID,
                Amount                  AS Amount,
                Type                    AS Type,
                TimeStamp               AS TimeStamp
            FROM BudgetCardTransaction
            WHERE BudgetCardID IN ({placeholders})
            ORDER BY BudgetCardID, TimeStamp;";

        var transRows = await _context.Db.QueryAsync<BudgetTransactionRow>(transSql, budgetIds.Cast<object>().ToArray());

        foreach (var transaction in transRows)
        {
            if (!byBudgetId.TryGetValue(transaction.BudgetCardID, out var parent))
                continue;

            parent.Transactions.Add(MapTransactionRowToModel(transaction, parent.ExchangeRate));
        }

        return models;
    }

    public async Task SaveBudgetCardModelDataAsync(BudgetCardModel model, long cardId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (cardId <= 0)
            throw new ArgumentException("Budget cards must be attached to a saved base card.", nameof(cardId));

        await _context.InitializeAsync();

        model.CardID = cardId;

        if (model.Id == 0)
        {
            await _context.Db.ExecuteAsync(
                @"INSERT INTO BudgetCard (CardID, Status, Description, Currency, ExchangeRate, StartDate, InitialBalance)
                  VALUES (?, ?, ?, ?, ?, ?, ?);",
                cardId,
                model.Status,
                model.Description,
                model.Currency,
                model.ExchangeRate,
                SerializeLocalDateTimeForDb(model.StartDate),
                model.InitialBalance);

            model.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }
        else
        {
            await _context.Db.ExecuteAsync(
                @"UPDATE BudgetCard
                  SET Status = ?,
                      Description = ?,
                      Currency = ?,
                      ExchangeRate = ?,
                      StartDate = ?,
                      InitialBalance = ?
                  WHERE CardID = ?;",
                model.Status,
                model.Description,
                model.Currency,
                model.ExchangeRate,
                SerializeLocalDateTimeForDb(model.StartDate),
                model.InitialBalance,
                cardId);
        }

        await SyncTopUpsAsync(model);
        await SyncTransactionsAsync(model);
    }

    private async Task LoadTopUpsAsync(BudgetCardModel model)
    {
        const string sql = @"
            SELECT
                BudgetCardScheduledTopUpID AS BudgetCardScheduledTopUpID,
                BudgetCardID               AS BudgetCardID,
                Amount                     AS Amount,
                TimeOfDaySeconds           AS TimeOfDaySeconds
            FROM BudgetCardScheduledTopUp
            WHERE BudgetCardID = ?
            ORDER BY BudgetCardScheduledTopUpID;";

        var rows = await _context.Db.QueryAsync<BudgetTopUpRow>(sql, model.Id);
        model.TopUps = new ObservableCollection<ScheduledTopUp>(rows.Select(MapTopUpRowToModel));
    }

    private async Task LoadTransactionsAsync(BudgetCardModel model)
    {
        const string sql = @"
            SELECT
                BudgetCardTransactionID AS BudgetCardTransactionID,
                BudgetCardID            AS BudgetCardID,
                Amount                  AS Amount,
                Type                    AS Type,
                TimeStamp               AS TimeStamp
            FROM BudgetCardTransaction
            WHERE BudgetCardID = ?
            ORDER BY TimeStamp;";

        var rows = await _context.Db.QueryAsync<BudgetTransactionRow>(sql, model.Id);
        model.Transactions = new ObservableCollection<BudgetTransaction>(
            rows.Select(row => MapTransactionRowToModel(row, model.ExchangeRate)));
    }

    private async Task SyncTopUpsAsync(BudgetCardModel model)
    {
        var existingTopUps = await _context.Db.QueryAsync<BudgetTopUpRow>(
            @"SELECT
                  BudgetCardScheduledTopUpID AS BudgetCardScheduledTopUpID,
                  BudgetCardID               AS BudgetCardID,
                  Amount                     AS Amount,
                  TimeOfDaySeconds           AS TimeOfDaySeconds
              FROM BudgetCardScheduledTopUp
              WHERE BudgetCardID = ?;",
            model.Id);

        foreach (var topup in model.TopUps)
        {
            if (topup.Id == 0)
            {
                await _context.Db.ExecuteAsync(
                    @"INSERT INTO BudgetCardScheduledTopUp (BudgetCardID, Amount, TimeOfDaySeconds)
                      VALUES (?, ?, ?);",
                    model.Id,
                    topup.Amount,
                    topup.TimeOfDay.TotalSeconds);

                topup.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                continue;
            }

            await _context.Db.ExecuteAsync(
                @"UPDATE BudgetCardScheduledTopUp
                  SET Amount = ?,
                      TimeOfDaySeconds = ?
                  WHERE BudgetCardScheduledTopUpID = ?;",
                topup.Amount,
                topup.TimeOfDay.TotalSeconds,
                topup.Id);

            var retained = existingTopUps.FirstOrDefault(x => x.BudgetCardScheduledTopUpID == topup.Id);
            if (retained != null)
                existingTopUps.Remove(retained);
        }

        foreach (var topupToDelete in existingTopUps)
        {
            await _context.Db.ExecuteAsync(
                "DELETE FROM BudgetCardScheduledTopUp WHERE BudgetCardScheduledTopUpID = ?;",
                topupToDelete.BudgetCardScheduledTopUpID);
        }
    }

    private async Task SyncTransactionsAsync(BudgetCardModel model)
    {
        var existingTransactions = await _context.Db.QueryAsync<BudgetTransactionRow>(
            @"SELECT
                  BudgetCardTransactionID AS BudgetCardTransactionID,
                  BudgetCardID            AS BudgetCardID,
                  Amount                  AS Amount,
                  Type                    AS Type,
                  TimeStamp               AS TimeStamp
              FROM BudgetCardTransaction
              WHERE BudgetCardID = ?;",
            model.Id);

        foreach (var transaction in model.Transactions)
        {
            if (transaction.Id == 0)
            {
                await _context.Db.ExecuteAsync(
                    @"INSERT INTO BudgetCardTransaction (BudgetCardID, Amount, Type, TimeStamp)
                      VALUES (?, ?, ?, ?);",
                    model.Id,
                    transaction.CurrencyAmount,
                    transaction.Type.ToString(),
                    SerializeInstantForDb(transaction.Timestamp));

                transaction.Id = (int)await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                continue;
            }

            await _context.Db.ExecuteAsync(
                @"UPDATE BudgetCardTransaction
                  SET Amount = ?,
                      Type = ?,
                      TimeStamp = ?
                  WHERE BudgetCardTransactionID = ?;",
                transaction.CurrencyAmount,
                transaction.Type.ToString(),
                SerializeInstantForDb(transaction.Timestamp),
                transaction.Id);

            var retained = existingTransactions.FirstOrDefault(x => x.BudgetCardTransactionID == transaction.Id);
            if (retained != null)
                existingTransactions.Remove(retained);
        }

        foreach (var transactionToDelete in existingTransactions)
        {
            await _context.Db.ExecuteAsync(
                "DELETE FROM UdmdTrans WHERE RelatedEntityType = ? AND RelatedEntityId = ?;",
                UdmdRelatedEntityTypes.BudgetTransaction,
                transactionToDelete.BudgetCardTransactionID);

            await _context.Db.ExecuteAsync(
                "DELETE FROM BudgetCardTransaction WHERE BudgetCardTransactionID = ?;",
                transactionToDelete.BudgetCardTransactionID);
        }
    }

    private BudgetCardModel MapBudgetRowToModel(BudgetCardJoinedRow row)
    {
        return new BudgetCardModel
        {
            Id = row.BudgetCardID,
            CardID = row.CardID,
            Title = row.Title ?? string.Empty,
            Tags = row.Tags ?? string.Empty,
            Status = row.Status ?? string.Empty,
            Description = row.Description ?? string.Empty,
            Currency = row.Currency ?? string.Empty,
            ExchangeRate = row.ExchangeRate,
            StartDate = ParseLocalDateTime(row.StartDate),
            InitialBalance = row.InitialBalance,
            TopUps = new ObservableCollection<ScheduledTopUp>(),
            Transactions = new ObservableCollection<BudgetTransaction>()
        };
    }

    private static ScheduledTopUp MapTopUpRowToModel(BudgetTopUpRow row)
    {
        return new ScheduledTopUp
        {
            Id = row.BudgetCardScheduledTopUpID,
            Amount = row.Amount,
            TimeOfDay = TimeSpan.FromSeconds(row.TimeOfDaySeconds)
        };
    }

    private BudgetTransaction MapTransactionRowToModel(BudgetTransactionRow row, double exchangeRate)
    {
        var type = ParseBudgetTransactionType(row.Type);
        var currencyAmount = row.Amount;

        return new BudgetTransaction
        {
            Id = row.BudgetCardTransactionID,
            CurrencyAmount = currencyAmount,
            Type = type,
            Timestamp = ParseInstantUtc(row.TimeStamp),
            GlobalValueAmount = type == BudgetTransactionType.CashIn
                ? currencyAmount * exchangeRate
                : 0
        };
    }

    private static BudgetTransactionType ParseBudgetTransactionType(string? type)
    {
        return Enum.TryParse<BudgetTransactionType>(type ?? string.Empty, true, out var parsed)
            ? parsed
            : BudgetTransactionType.Spend;
    }

    private static DateTime ParseLocalDateTime(string value)
    {
        return LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime;
    }

    private DateTime ParseInstantUtc(string value)
    {
        return LegacyTimeReader.ReadInstantUtc(value, _timeZoneService).UtcInstant;
    }

    private static string SerializeLocalDateTimeForDb(DateTime value)
    {
        return StrictTimeSerializer.SerializeLocalDateTime(value);
    }

    private string SerializeInstantForDb(DateTime value)
    {
        return StrictTimeSerializer.SerializeUtcInstant(ToUtcInstantForWrite(value));
    }

    private DateTime ToUtcInstantForWrite(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? StrictTimeSerializer.RequireUtcInstant(value, nameof(value))
            : _timeZoneService.ToUtcFromLocal(value);
    }

    private sealed class BudgetCardJoinedRow
    {
        public int BudgetCardID { get; set; }
        public long CardID { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? Currency { get; set; }
        public double ExchangeRate { get; set; }
        public string StartDate { get; set; } = "";
        public double InitialBalance { get; set; }
    }

    private sealed class BudgetTopUpRow
    {
        public int BudgetCardScheduledTopUpID { get; set; }
        public int BudgetCardID { get; set; }
        public double Amount { get; set; }
        public double TimeOfDaySeconds { get; set; }
    }

    private sealed class BudgetTransactionRow
    {
        public int BudgetCardTransactionID { get; set; }
        public int BudgetCardID { get; set; }
        public double Amount { get; set; }
        public string? Type { get; set; }
        public string TimeStamp { get; set; } = "";
    }
}
