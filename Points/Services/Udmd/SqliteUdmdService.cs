using Points.Models;
using Points.Services;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using System.Globalization;

namespace Points.Services.Udmd;

public sealed class SqliteUdmdService : IUdmdService
{
    private readonly ISqliteConnectionContext _context;

    public SqliteUdmdService(ISqliteConnectionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<UdmdConfigModel>> GetUdmdConfigsForCardAsync(long cardId)
    {
        await _context.InitializeAsync();

        return await _context.Db.QueryAsync<UdmdConfigModel>(
            @"SELECT UdmdConfigID, CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive
              FROM UdmdConfig
              WHERE CardID = ?
              ORDER BY DisplayOrder, FieldName;",
            cardId);
    }

    public async Task<List<UdmdConfigModel>> GetActiveUdmdConfigsForCardAsync(long cardId)
    {
        await _context.InitializeAsync();

        return await _context.Db.QueryAsync<UdmdConfigModel>(
            @"SELECT UdmdConfigID, CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive
              FROM UdmdConfig
              WHERE CardID = ?
                AND IsActive = 1
              ORDER BY DisplayOrder, FieldName;",
            cardId);
    }

    public async Task<UdmdConfigModel> SaveUdmdConfigAsync(UdmdConfigModel config)
    {
        await _context.InitializeAsync();

        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (config.CardID <= 0)
            throw new InvalidOperationException("UDMD config must be attached to a saved card.");

        await EnsureUdmdCardExistsAsync(config.CardID);

        config.FieldName = (config.FieldName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(config.FieldName))
            throw new InvalidOperationException("UDMD field name is required.");

        config.FieldType = NormalizeUdmdFieldType(config.FieldType).ToString();

        if (config.UdmdConfigID == 0)
        {
            await _context.Db.ExecuteAsync(
                @"INSERT INTO UdmdConfig (CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive)
                  VALUES (?, ?, ?, ?, ?, ?);",
                config.CardID,
                config.FieldName,
                config.FieldType,
                config.IsRequired ? 1 : 0,
                config.DisplayOrder,
                config.IsActive ? 1 : 0);

            config.UdmdConfigID = await _context.Db.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }
        else
        {
            await _context.Db.ExecuteAsync(
                @"UPDATE UdmdConfig
                  SET CardID = ?,
                      FieldName = ?,
                      FieldType = ?,
                      IsRequired = ?,
                      DisplayOrder = ?,
                      IsActive = ?
                  WHERE UdmdConfigID = ?;",
                config.CardID,
                config.FieldName,
                config.FieldType,
                config.IsRequired ? 1 : 0,
                config.DisplayOrder,
                config.IsActive ? 1 : 0,
                config.UdmdConfigID);
        }

        return config;
    }

    public async Task DeleteOrDeactivateUdmdConfigAsync(long udmdConfigId)
    {
        await _context.InitializeAsync();

        await _context.Db.ExecuteAsync(
            "UPDATE UdmdConfig SET IsActive = 0 WHERE UdmdConfigID = ?;",
            udmdConfigId);
    }

    public async Task<List<UdmdDropdownModel>> GetDropdownValuesAsync(long udmdConfigId)
    {
        await _context.InitializeAsync();

        return await _context.Db.QueryAsync<UdmdDropdownModel>(
            @"SELECT UdmdDropdownID, UdmdConfigID, DropdownValue, DisplayOrder, IsActive
              FROM UdmdDropdown
              WHERE UdmdConfigID = ?
                AND IsActive = 1
              ORDER BY DisplayOrder, DropdownValue;",
            udmdConfigId);
    }

    public async Task SaveDropdownValuesAsync(long udmdConfigId, IEnumerable<string> values)
    {
        await _context.InitializeAsync();

        var config = await GetUdmdConfigByIdAsync(udmdConfigId);
        if (config == null)
            throw new InvalidOperationException("UDMD dropdown config was not found.");

        var normalizedValues = (values ?? Enumerable.Empty<string>())
            .Select(v => (v ?? "").Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _context.Db.QueryAsync<UdmdDropdownModel>(
            @"SELECT UdmdDropdownID, UdmdConfigID, DropdownValue, DisplayOrder, IsActive
              FROM UdmdDropdown
              WHERE UdmdConfigID = ?;",
            udmdConfigId);

        await _context.RunInTransactionAsync(conn =>
        {
            for (var i = 0; i < normalizedValues.Count; i++)
            {
                var value = normalizedValues[i];
                var match = existing.FirstOrDefault(x =>
                    string.Equals(x.DropdownValue, value, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    conn.Execute(
                        @"INSERT INTO UdmdDropdown (UdmdConfigID, DropdownValue, DisplayOrder, IsActive)
                          VALUES (?, ?, ?, 1);",
                        udmdConfigId,
                        value,
                        i);
                }
                else
                {
                    conn.Execute(
                        @"UPDATE UdmdDropdown
                          SET DropdownValue = ?, DisplayOrder = ?, IsActive = 1
                          WHERE UdmdDropdownID = ?;",
                        value,
                        i,
                        match.UdmdDropdownID);
                }
            }

            foreach (var stale in existing.Where(x =>
                         !normalizedValues.Contains(x.DropdownValue, StringComparer.OrdinalIgnoreCase)))
            {
                conn.Execute(
                    "UPDATE UdmdDropdown SET IsActive = 0 WHERE UdmdDropdownID = ?;",
                    stale.UdmdDropdownID);
            }
        });
    }

    public async Task SaveMetadataForEntityAsync(
        long cardId,
        string relatedEntityType,
        long relatedEntityId,
        IEnumerable<UdmdValueInput> values)
    {
        await _context.InitializeAsync();

        if (!UdmdRelatedEntityTypes.IsSupported(relatedEntityType))
            throw new InvalidOperationException($"Unsupported UDMD related entity type: {relatedEntityType}");

        if (cardId <= 0)
            throw new InvalidOperationException("UDMD metadata must be attached to a saved card.");

        if (relatedEntityId <= 0)
            throw new InvalidOperationException("UDMD metadata must be attached to a saved parent row.");

        await EnsureUdmdCardExistsAsync(cardId);
        await EnsureUdmdRelatedParentExistsAsync(cardId, relatedEntityType, relatedEntityId);

        var configs = await GetUdmdConfigsForCardAsync(cardId);
        var configById = configs.ToDictionary(x => x.UdmdConfigID);
        var distinctInputs = new Dictionary<long, UdmdValueInput>();

        foreach (var input in values ?? Enumerable.Empty<UdmdValueInput>())
        {
            if (input == null)
                continue;

            if (distinctInputs.ContainsKey(input.UdmdConfigID))
                throw new InvalidOperationException("UDMD metadata contains duplicate values for the same field.");

            distinctInputs[input.UdmdConfigID] = input;
        }

        foreach (var required in configs.Where(x => x.IsActive && x.IsRequired))
        {
            if (!distinctInputs.TryGetValue(required.UdmdConfigID, out var input) ||
                string.IsNullOrWhiteSpace(input.FieldValue))
            {
                throw new InvalidOperationException($"Required metadata field '{required.FieldName}' is missing.");
            }
        }

        var normalizedRows = new List<(UdmdConfigModel Config, string FieldValue)>();

        foreach (var input in distinctInputs.Values)
        {
            if (!configById.TryGetValue(input.UdmdConfigID, out var config))
                throw new InvalidOperationException("UDMD metadata references a field that does not belong to this card.");

            if (!config.IsActive)
                throw new InvalidOperationException($"UDMD field '{config.FieldName}' is inactive.");

            if (string.IsNullOrWhiteSpace(input.FieldValue))
                continue;

            var normalizedValue = await NormalizeUdmdFieldValueAsync(config, input.FieldValue);
            normalizedRows.Add((config, normalizedValue));
        }

        if (normalizedRows.Count == 0)
            return;

        await _context.RunInTransactionAsync(conn =>
        {
            foreach (var row in normalizedRows)
            {
                var existingId = conn.ExecuteScalar<long>(
                    @"SELECT UdmdTransID
                      FROM UdmdTrans
                      WHERE RelatedEntityType = ?
                        AND RelatedEntityId = ?
                        AND UdmdConfigID = ?
                      LIMIT 1;",
                    relatedEntityType,
                    relatedEntityId,
                    row.Config.UdmdConfigID);

                if (existingId > 0)
                {
                    conn.Execute(
                        @"UPDATE UdmdTrans
                          SET CardID = ?, FieldValue = ?
                          WHERE UdmdTransID = ?;",
                        cardId,
                        row.FieldValue,
                        existingId);
                }
                else
                {
                    conn.Execute(
                        @"INSERT INTO UdmdTrans (CardID, UdmdConfigID, RelatedEntityType, RelatedEntityId, FieldValue)
                          VALUES (?, ?, ?, ?, ?);",
                        cardId,
                        row.Config.UdmdConfigID,
                        relatedEntityType,
                        relatedEntityId,
                        row.FieldValue);
                }
            }
        });
    }

    public async Task<List<UdmdTransModel>> GetMetadataForEntityAsync(string relatedEntityType, long relatedEntityId)
    {
        await _context.InitializeAsync();

        if (!UdmdRelatedEntityTypes.IsSupported(relatedEntityType))
            throw new InvalidOperationException($"Unsupported UDMD related entity type: {relatedEntityType}");

        return await _context.Db.QueryAsync<UdmdTransModel>(
            @"SELECT t.UdmdTransID,
                     t.CardID,
                     t.UdmdConfigID,
                     t.RelatedEntityType,
                     t.RelatedEntityId,
                     t.FieldValue,
                     c.FieldName,
                     c.FieldType
              FROM UdmdTrans t
              JOIN UdmdConfig c ON c.UdmdConfigID = t.UdmdConfigID
              WHERE t.RelatedEntityType = ?
                AND t.RelatedEntityId = ?
              ORDER BY c.DisplayOrder, c.FieldName;",
            relatedEntityType,
            relatedEntityId);
    }

    public async Task<List<UdmdTransModel>> GetMetadataForCardAsync(long cardId)
    {
        await _context.InitializeAsync();

        return await _context.Db.QueryAsync<UdmdTransModel>(
            @"SELECT t.UdmdTransID,
                     t.CardID,
                     t.UdmdConfigID,
                     t.RelatedEntityType,
                     t.RelatedEntityId,
                     t.FieldValue,
                     c.FieldName,
                     c.FieldType
              FROM UdmdTrans t
              JOIN UdmdConfig c ON c.UdmdConfigID = t.UdmdConfigID
              WHERE t.CardID = ?
              ORDER BY t.RelatedEntityType, t.RelatedEntityId, c.DisplayOrder, c.FieldName;",
            cardId);
    }

    public Task SaveActivityMetadataAsync(long cardId, long activityId, IEnumerable<UdmdValueInput> values)
    {
        return SaveMetadataForEntityAsync(cardId, UdmdRelatedEntityTypes.Activity, activityId, values);
    }

    public Task SaveBudgetTransactionMetadataAsync(long cardId, long budgetTransactionId, IEnumerable<UdmdValueInput> values)
    {
        return SaveMetadataForEntityAsync(cardId, UdmdRelatedEntityTypes.BudgetTransaction, budgetTransactionId, values);
    }

    public Task SaveTrackerValueMetadataAsync(long cardId, long trackerValueId, IEnumerable<UdmdValueInput> values)
    {
        return SaveMetadataForEntityAsync(cardId, UdmdRelatedEntityTypes.TrackerValue, trackerValueId, values);
    }

    private async Task EnsureUdmdCardExistsAsync(long cardId)
    {
        var exists = await _context.Db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Card WHERE CardID = ?;",
            cardId);

        if (exists <= 0)
            throw new InvalidOperationException("UDMD card was not found.");
    }

    private async Task<UdmdConfigModel?> GetUdmdConfigByIdAsync(long udmdConfigId)
    {
        return (await _context.Db.QueryAsync<UdmdConfigModel>(
                @"SELECT UdmdConfigID, CardID, FieldName, FieldType, IsRequired, DisplayOrder, IsActive
                  FROM UdmdConfig
                  WHERE UdmdConfigID = ?
                  LIMIT 1;",
                udmdConfigId))
            .FirstOrDefault();
    }

    private static UdmdFieldType NormalizeUdmdFieldType(string? fieldType)
    {
        return Enum.TryParse<UdmdFieldType>(fieldType ?? "", true, out var parsed)
            ? parsed
            : UdmdFieldType.Text;
    }

    private async Task<string> NormalizeUdmdFieldValueAsync(UdmdConfigModel config, string rawValue)
    {
        var value = (rawValue ?? "").Trim();
        var fieldType = NormalizeUdmdFieldType(config.FieldType);

        switch (fieldType)
        {
            case UdmdFieldType.Dropdown:
                var dropdowns = await GetDropdownValuesAsync(config.UdmdConfigID);
                var match = dropdowns.FirstOrDefault(d =>
                    string.Equals(d.DropdownValue, value, StringComparison.Ordinal));

                if (match == null)
                    throw new InvalidOperationException($"'{value}' is not an allowed value for '{config.FieldName}'.");

                return match.DropdownValue;

            case UdmdFieldType.Number:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantNumber) &&
                    !double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out invariantNumber))
                {
                    throw new InvalidOperationException($"'{config.FieldName}' must be a number.");
                }

                return invariantNumber.ToString("G17", CultureInfo.InvariantCulture);

            case UdmdFieldType.Date:
                try
                {
                    var localDateTime = LegacyTimeReader.ReadLocalDateTime(value).LocalDateTime;
                    return StrictTimeSerializer.SerializeLocalDateTime(localDateTime);
                }
                catch
                {
                    throw new InvalidOperationException($"'{config.FieldName}' must be a date.");
                }

            case UdmdFieldType.Boolean:
                if (TryParseUdmdBoolean(value, out var boolValue))
                    return boolValue ? "true" : "false";

                throw new InvalidOperationException($"'{config.FieldName}' must be true or false.");

            case UdmdFieldType.Image:
                if (!UdmdImageFileStore.IsSafeStoredFileName(value))
                    throw new InvalidOperationException($"'{config.FieldName}' must be a stored image filename.");

                if (!UdmdImageFileStore.ImageExists(config.CardID, value))
                    throw new InvalidOperationException($"The image for '{config.FieldName}' could not be found.");

                return value;

            case UdmdFieldType.Text:
            default:
                return value;
        }
    }

    private static bool TryParseUdmdBoolean(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
            return true;

        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
                result = true;
                return true;
            case "0":
            case "no":
            case "n":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private async Task EnsureUdmdRelatedParentExistsAsync(long cardId, string relatedEntityType, long relatedEntityId)
    {
        var count = relatedEntityType switch
        {
            UdmdRelatedEntityTypes.Activity => await _context.Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Activity WHERE ActivityID = ? AND CardID = ?;",
                relatedEntityId,
                cardId),

            UdmdRelatedEntityTypes.BudgetTransaction => await _context.Db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM BudgetCardTransaction t
                  JOIN BudgetCard b ON b.BudgetCardID = t.BudgetCardID
                  WHERE t.BudgetCardTransactionID = ?
                    AND b.CardID = ?;",
                relatedEntityId,
                cardId),

            UdmdRelatedEntityTypes.TrackerValue => await _context.Db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM TrackerValue WHERE TrackerValueID = ? AND CardID = ?;",
                relatedEntityId,
                cardId),

            _ => 0
        };

        if (count <= 0)
            throw new InvalidOperationException("UDMD related parent row was not found for this card.");
    }
}
