using Points.Services.Sqlite;
using Points.Global;
using Points.Models.DbModels;
using Points.Services.Persistence;
using System.Globalization;

namespace Points.Services.Settings
{
    public sealed class SqliteSettingsService : ISettingsService
    {
        private readonly ISqliteConnectionContext _context;

        public SqliteSettingsService(ISqliteConnectionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task SaveBuiltInSettingDefinitionsAsync(bool initializeContext = true)
        {
            if (initializeContext)
                await _context.InitializeAsync();

            var settingDefinitions = SettingKeys.GetBuiltInSettingDefinitions();
            await SaveSettingDefinitionsAsync(settingDefinitions);
        }

        public async Task<List<AcquiredSetting>> GetSettingsAsync()
        {
            await _context.InitializeAsync();

            var settingRows = await _context.Db.QueryAsync<SettingRow>(
                "SELECT * FROM Setting ORDER BY Category, SortOrder, SettingKey;");

            var acquiredSettings = new List<AcquiredSetting>();

            foreach (var row in settingRows)
            {
                var acquiredSetting = GetAcquiredSettingFromRow(row);
                acquiredSettings.Add(acquiredSetting);
            }

            return acquiredSettings;
        }

        public async Task<Dictionary<string, AcquiredSetting>> GetSettingsByKeyAsync()
        {
            var settings = await GetSettingsAsync();
            return settings.ToDictionary(x => x.SettingKey, x => x);
        }

        public async Task SetStringSettingAsync(string settingKey, string value)
        {
            await SetSettingValueAsync(settingKey, value ?? string.Empty, SettingValueTypes.String);
        }

        public async Task SetBoolSettingAsync(string settingKey, bool value)
        {
            await SetSettingValueAsync(settingKey, FormatBoolSettingValue(value), SettingValueTypes.Bool);
        }

        public async Task SetIntSettingAsync(string settingKey, int value)
        {
            await SetSettingValueAsync(settingKey, FormatIntSettingValue(value), SettingValueTypes.Int);
        }

        public async Task SetNullableIntSettingAsync(string settingKey, int? value)
        {
            await SetSettingValueAsync(settingKey, FormatNullableIntSettingValue(value), SettingValueTypes.NullableInt);
        }

        public async Task SetDoubleSettingAsync(string settingKey, double value)
        {
            await SetSettingValueAsync(settingKey, FormatDoubleSettingValue(value), SettingValueTypes.Double);
        }

        private static void ValidateSettingDefinitions(List<SettingDefinition> settingDefinitions)
        {
            var duplicateKeys = settingDefinitions
                .GroupBy(x => x.SettingKey)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateKeys.Any())
            {
                throw new InvalidOperationException(
                    $"Duplicate setting keys found: {string.Join(", ", duplicateKeys)}");
            }
        }

        private async Task SaveSettingDefinitionsAsync(List<SettingDefinition> settingDefinitions)
        {
            ValidateSettingDefinitions(settingDefinitions);

            var existingSettingsInDb = await _context.Db.QueryAsync<SettingRow>("SELECT * FROM Setting");

            foreach (var definition in settingDefinitions)
            {
                var existingSetting = existingSettingsInDb.FirstOrDefault(x => x.SettingKey == definition.SettingKey);

                if (existingSetting == null)
                {
                    await _context.Db.ExecuteAsync(
                        @"INSERT INTO Setting
                            (SettingKey, SettingValue, ValueType, Category, DisplayName, Description, IsUserEditable, SortOrder)
                          VALUES (?, ?, ?, ?, ?, ?, ?, ?);",
                        definition.SettingKey,
                        definition.DefaultValue,
                        definition.ValueType,
                        definition.Category,
                        definition.DisplayName,
                        definition.Description,
                        definition.IsUserEditable ? 1 : 0,
                        definition.SortOrder);
                }
                else
                {
                    await _context.Db.ExecuteAsync(
                        @"UPDATE Setting
                          SET ValueType = ?,
                              Category = ?,
                              DisplayName = ?,
                              Description = ?,
                              IsUserEditable = ?,
                              SortOrder = ?
                          WHERE SettingKey = ?;",
                        definition.ValueType,
                        definition.Category,
                        definition.DisplayName,
                        definition.Description,
                        definition.IsUserEditable ? 1 : 0,
                        definition.SortOrder,
                        definition.SettingKey);

                    existingSettingsInDb.Remove(existingSetting);
                }
            }

            foreach (var settingToDelete in existingSettingsInDb)
            {
                await _context.Db.ExecuteAsync(
                    "DELETE FROM Setting WHERE SettingKey = ?;",
                    settingToDelete.SettingKey);
            }
        }

        private AcquiredSetting GetAcquiredSettingFromRow(SettingRow row)
        {
            return row.ValueType switch
            {
                SettingValueTypes.String => GetStringSettingFromRow(row),
                SettingValueTypes.Bool => GetBoolSettingFromRow(row),
                SettingValueTypes.Int => GetIntSettingFromRow(row),
                SettingValueTypes.NullableInt => GetNullableIntSettingFromRow(row),
                SettingValueTypes.Double => GetDoubleSettingFromRow(row),
                _ => throw new InvalidOperationException(
                    $"Unsupported setting value type '{row.ValueType}' for setting '{row.SettingKey}'.")
            };
        }

        private static AcquiredSetting GetStringSettingFromRow(SettingRow row)
        {
            return CreateBaseAcquiredSetting(row, row.SettingValue, stringValue: row.SettingValue);
        }

        private static AcquiredSetting GetBoolSettingFromRow(SettingRow row)
        {
            if (!TryParseBoolSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.Bool}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, boolValue: parsedValue);
        }

        private static AcquiredSetting GetIntSettingFromRow(SettingRow row)
        {
            if (!TryParseIntSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.Int}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, intValue: parsedValue);
        }

        private static AcquiredSetting GetNullableIntSettingFromRow(SettingRow row)
        {
            if (!TryParseNullableIntSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.NullableInt}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, intValue: parsedValue);
        }

        private static AcquiredSetting GetDoubleSettingFromRow(SettingRow row)
        {
            if (!TryParseDoubleSettingValue(row.SettingValue, out var parsedValue))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{SettingValueTypes.Double}' but value '{row.SettingValue}' could not be parsed.");
            }

            return CreateBaseAcquiredSetting(row, row.SettingValue, doubleValue: parsedValue);
        }

        private static AcquiredSetting CreateBaseAcquiredSetting(
            SettingRow row,
            string rawValue,
            string? stringValue = null,
            bool? boolValue = null,
            int? intValue = null,
            double? doubleValue = null)
        {
            return new AcquiredSetting
            {
                SettingKey = row.SettingKey,
                ValueType = row.ValueType,
                RawValue = rawValue,

                StringValue = stringValue,
                BoolValue = boolValue,
                IntValue = intValue,
                DoubleValue = doubleValue,

                Category = row.Category,
                DisplayName = row.DisplayName,
                Description = row.Description,
                IsUserEditable = row.IsUserEditable == 1,
                SortOrder = row.SortOrder
            };
        }

        private static bool TryParseBoolSettingValue(string value, out bool parsedValue)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                parsedValue = true;
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                parsedValue = false;
                return true;
            }

            parsedValue = false;
            return false;
        }

        private static bool TryParseIntSettingValue(string value, out int parsedValue)
        {
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsedValue);
        }

        private static bool TryParseNullableIntSettingValue(string value, out int? parsedValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                parsedValue = null;
                return true;
            }

            if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedInt))
            {
                parsedValue = parsedInt;
                return true;
            }

            parsedValue = null;
            return false;
        }

        private static bool TryParseDoubleSettingValue(string value, out double parsedValue)
        {
            return double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out parsedValue);
        }

        private async Task SetSettingValueAsync(string settingKey, string settingValue, string expectedValueType)
        {
            await _context.InitializeAsync();

            var existingRows = await _context.Db.QueryAsync<SettingRow>(
                "SELECT * FROM Setting WHERE SettingKey = ?;",
                settingKey);

            var existingRow = existingRows.FirstOrDefault();

            if (existingRow == null)
            {
                throw new InvalidOperationException(
                    $"Cannot set value for setting '{settingKey}' because no Setting row exists for that key.");
            }

            ValidateSettingValueType(existingRow, expectedValueType);

            await _context.Db.ExecuteAsync(
                "UPDATE Setting SET SettingValue = ? WHERE SettingKey = ?;",
                settingValue,
                settingKey);
        }

        private static void ValidateSettingValueType(SettingRow row, string expectedValueType)
        {
            if (!string.Equals(row.ValueType, expectedValueType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Setting '{row.SettingKey}' has ValueType '{row.ValueType}', but code attempted to write it as '{expectedValueType}'.");
            }
        }

        private static string FormatBoolSettingValue(bool value)
        {
            return value ? "true" : "false";
        }

        private static string FormatIntSettingValue(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatNullableIntSettingValue(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string FormatDoubleSettingValue(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class SettingRow
        {
            public string SettingKey { get; set; } = string.Empty;
            public string SettingValue { get; set; } = string.Empty;
            public string ValueType { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int IsUserEditable { get; set; }
            public int SortOrder { get; set; }
        }
    }
}
