using Points.Models.DbModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Global
{
    public static class SettingKeys
    {
        public const string HardModeEnabled = "HardModeEnabled";
        public const string HardModeDamagePerMinuteValue = "HardModeDamagePerMinuteValue";
        public const string StatusConditionsEnabled = "StatusConditionsEnabled";
        public const string CurrentlyAppliedStatusConditionId = "CurrentlyAppliedStatusConditionId";
        public const string SelectedThemeId = "SelectedThemeId";

        public const string DashboardActive = "DashboardActive";
        public const string DashboardScreenOrder = "DashboardScreenOrder";
        public const string MainQuestActive = "MainQuestActive";
        public const string MainQuestScreenOrder = "MainQuestScreenOrder";
        public const string MissionActive = "MissionActive";
        public const string MissionScreenOrder = "MissionScreenOrder";
        public const string BudgetsActive = "BudgetsActive";
        public const string BudgetsScreenOrder = "BudgetsScreenOrder";
        public const string AchievementsActive = "AchievementsActive";
        public const string AchievementsScreenOrder = "AchievementsScreenOrder";
        public const string ArcsActive = "ArcsActive";
        public const string ArcsScreenOrder = "ArcsScreenOrder";
        public const string PlannersActive = "PlannersActive";
        public const string PlannersScreenOrder = "PlannersScreenOrder";

        public const string LocksActive = "LocksActive";
        public const string SchedulesActive = "SchedulesActive";
        public const string ValueRatesActive = "ValueRatesActive";
        public const string CashInActive = "CashInActive";

        public static List<SettingDefinition> GetBuiltInSettingDefinitions()
        {
            return new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    SettingKey = SettingKeys.HardModeEnabled,
                    DefaultValue = "false",
                    ValueType = SettingValueTypes.Bool,
                    Category = "Multipliers",
                    DisplayName = "Hard Mode",
                    Description = "When enabled, idle time applies a penalty while no activity is active.",
                    IsUserEditable = true,
                    SortOrder = 10
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.HardModeDamagePerMinuteValue,
                    DefaultValue = "0",
                    ValueType = SettingValueTypes.Double,
                    Category = "Multipliers",
                    DisplayName = "Idle Penalty Per Minute",
                    Description = "Points applied per idle minute. Stored as a negative value.",
                    IsUserEditable = true,
                    SortOrder = 20
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.StatusConditionsEnabled,
                    DefaultValue = "false",
                    ValueType = SettingValueTypes.Bool,
                    Category = "Multipliers",
                    DisplayName = "Status Conditions Enabled",
                    Description = "Enables status-based point multipliers.",
                    IsUserEditable = true,
                    SortOrder = 30
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.CurrentlyAppliedStatusConditionId,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.NullableInt,
                    Category = "Multipliers",
                    DisplayName = "Current Status Condition",
                    Description = "The currently applied status condition identifier.",
                    IsUserEditable = false,
                    SortOrder = 40
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.SelectedThemeId,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.NullableInt,
                    Category = "Appearance",
                    DisplayName = "Selected Theme",
                    Description = "The currently selected theme identifier.",
                    IsUserEditable = false,
                    SortOrder = 10
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.DashboardActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Dashboard Active",
                    Description = "Whether the Dashboard module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 10
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.DashboardScreenOrder,
                    DefaultValue = "1",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Dashboard Screen Order",
                    Description = "Display order for the Dashboard module.",
                    IsUserEditable = true,
                    SortOrder = 20
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MainQuestActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Main Quest Active",
                    Description = "Whether the Main Quest module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 30
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MainQuestScreenOrder,
                    DefaultValue = "2",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Main Quest Screen Order",
                    Description = "Display order for the Main Quest module.",
                    IsUserEditable = true,
                    SortOrder = 40
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Mission Active",
                    Description = "Whether the Mission module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 50
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionScreenOrder,
                    DefaultValue = "3",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Mission Screen Order",
                    Description = "Display order for the Mission module.",
                    IsUserEditable = true,
                    SortOrder = 60
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.BudgetsActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Budgets Active",
                    Description = "Whether the Budgets module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 70
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.BudgetsScreenOrder,
                    DefaultValue = "4",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Budgets Screen Order",
                    Description = "Display order for the Budgets module.",
                    IsUserEditable = true,
                    SortOrder = 80
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.AchievementsActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Achievements Active",
                    Description = "Whether the Achievements module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 90
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.AchievementsScreenOrder,
                    DefaultValue = "5",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Achievements Screen Order",
                    Description = "Display order for the Achievements module.",
                    IsUserEditable = true,
                    SortOrder = 100
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.ArcsActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Arcs Active",
                    Description = "Whether the Arcs module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 110
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.ArcsScreenOrder,
                    DefaultValue = "6",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Arcs Screen Order",
                    Description = "Display order for the Arcs module.",
                    IsUserEditable = true,
                    SortOrder = 120
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.PlannersActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Planners Active",
                    Description = "Whether the Planners module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 130
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.PlannersScreenOrder,
                    DefaultValue = "7",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Planners Screen Order",
                    Description = "Display order for the Planners module.",
                    IsUserEditable = true,
                    SortOrder = 140
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.LocksActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Locks Active",
                    Description = "Whether Locks features are enabled.",
                    IsUserEditable = true,
                    SortOrder = 150
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.SchedulesActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Schedules Active",
                    Description = "Whether Schedules features are enabled.",
                    IsUserEditable = true,
                    SortOrder = 160
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.ValueRatesActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Value Rates Active",
                    Description = "Whether Value Rates features are enabled.",
                    IsUserEditable = true,
                    SortOrder = 170
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.CashInActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Cash In Active",
                    Description = "Whether Cash In features are enabled.",
                    IsUserEditable = true,
                    SortOrder = 180
                }
            };
        }
    }

    public sealed class SettingDefinition
    {
        public string SettingKey { get; init; } = string.Empty;
        public string DefaultValue { get; init; } = string.Empty;
        public string ValueType { get; init; } = SettingValueTypes.String;
        public string Category { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool IsUserEditable { get; init; } = true;
        public int SortOrder { get; init; }
    }

    public sealed class AcquiredSetting
    {
        public string SettingKey { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;

        public string RawValue { get; set; } = string.Empty;

        public string? StringValue { get; set; }
        public bool? BoolValue { get; set; }
        public int? IntValue { get; set; }
        public double? DoubleValue { get; set; }

        public string Category { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsUserEditable { get; set; }
        public int SortOrder { get; set; }
    }

    public static class SettingsProvider
    {
        private static readonly Dictionary<string, AcquiredSetting> _settingsByKey = new(StringComparer.Ordinal);

        public static void Initialize(List<AcquiredSetting> settings)
        {
            _settingsByKey.Clear();

            if (settings == null)
                return;

            foreach (var setting in settings)
            {
                if (string.IsNullOrWhiteSpace(setting.SettingKey))
                    continue;

                _settingsByKey[setting.SettingKey] = setting;
            }
        }

        // -----------------------
        // Multipliers
        // -----------------------

        public static bool HardModeEnabled => GetBool(SettingKeys.HardModeEnabled, false);
        public static double HardModeDamagePerMinuteValue => GetDouble(SettingKeys.HardModeDamagePerMinuteValue, 0d);
        public static bool StatusConditionsEnabled => GetBool(SettingKeys.StatusConditionsEnabled, false);
        public static int? CurrentlyAppliedStatusConditionId => GetNullableInt(SettingKeys.CurrentlyAppliedStatusConditionId);
        public static int? SelectedThemeId => GetNullableInt(SettingKeys.SelectedThemeId);

        // -----------------------
        // Modules and screen order
        // -----------------------

        public static bool DashboardActive => GetBool(SettingKeys.DashboardActive, true);
        public static int DashboardScreenOrder => GetInt(SettingKeys.DashboardScreenOrder, 1);

        public static bool MainQuestActive => GetBool(SettingKeys.MainQuestActive, true);
        public static int MainQuestScreenOrder => GetInt(SettingKeys.MainQuestScreenOrder, 2);

        public static bool MissionActive => GetBool(SettingKeys.MissionActive, true);
        public static int MissionScreenOrder => GetInt(SettingKeys.MissionScreenOrder, 3);

        public static bool BudgetsActive => GetBool(SettingKeys.BudgetsActive, true);
        public static int BudgetsScreenOrder => GetInt(SettingKeys.BudgetsScreenOrder, 4);

        public static bool AchievementsActive => GetBool(SettingKeys.AchievementsActive, true);
        public static int AchievementsScreenOrder => GetInt(SettingKeys.AchievementsScreenOrder, 5);

        public static bool ArcsActive => GetBool(SettingKeys.ArcsActive, true);
        public static int ArcsScreenOrder => GetInt(SettingKeys.ArcsScreenOrder, 6);

        public static bool PlannersActive => GetBool(SettingKeys.PlannersActive, true);
        public static int PlannersScreenOrder => GetInt(SettingKeys.PlannersScreenOrder, 7);

        // -----------------------
        // Feature flags
        // -----------------------

        public static bool IsLocksEnabled => GetBool(SettingKeys.LocksActive, true);
        public static bool IsSchedulesEnabled => GetBool(SettingKeys.SchedulesActive, true);
        public static bool IsValueRatesEnabled => GetBool(SettingKeys.ValueRatesActive, true);
        public static bool IsCashInEnabled => GetBool(SettingKeys.CashInActive, true);

        // -----------------------
        // Public helpers
        // -----------------------

        public static string GetString(string key, string defaultValue = "")
            => TryGetSetting(key, out var setting)
                ? (!string.IsNullOrWhiteSpace(setting.StringValue) ? setting.StringValue! : setting.RawValue ?? defaultValue)
                : defaultValue;

        public static bool GetBool(string key, bool defaultValue = false)
        {
            if (!TryGetSetting(key, out var setting))
                return defaultValue;

            if (setting.BoolValue.HasValue)
                return setting.BoolValue.Value;

            if (!string.IsNullOrWhiteSpace(setting.RawValue) &&
                bool.TryParse(setting.RawValue, out var parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            if (!TryGetSetting(key, out var setting))
                return defaultValue;

            if (setting.IntValue.HasValue)
                return setting.IntValue.Value;

            if (!string.IsNullOrWhiteSpace(setting.RawValue) &&
                int.TryParse(setting.RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        public static int? GetNullableInt(string key)
        {
            if (!TryGetSetting(key, out var setting))
                return null;

            if (setting.IntValue.HasValue)
                return setting.IntValue.Value;

            if (!string.IsNullOrWhiteSpace(setting.RawValue) &&
                int.TryParse(setting.RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        public static double GetDouble(string key, double defaultValue = 0d)
        {
            if (!TryGetSetting(key, out var setting))
                return defaultValue;

            if (setting.DoubleValue.HasValue)
                return setting.DoubleValue.Value;

            if (!string.IsNullOrWhiteSpace(setting.RawValue) &&
                double.TryParse(setting.RawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        public static void UpdateString(string key, string value)
        {
            var setting = GetOrCreate(key, SettingValueTypes.String);
            setting.RawValue = value ?? string.Empty;
            setting.StringValue = value;
            setting.BoolValue = null;
            setting.IntValue = null;
            setting.DoubleValue = null;
        }

        public static void UpdateBool(string key, bool value)
        {
            var setting = GetOrCreate(key, SettingValueTypes.Bool);
            setting.RawValue = value.ToString().ToLowerInvariant();
            setting.StringValue = null;
            setting.BoolValue = value;
            setting.IntValue = null;
            setting.DoubleValue = null;
        }

        public static void UpdateInt(string key, int value)
        {
            var setting = GetOrCreate(key, SettingValueTypes.Int);
            setting.RawValue = value.ToString(CultureInfo.InvariantCulture);
            setting.StringValue = null;
            setting.BoolValue = null;
            setting.IntValue = value;
            setting.DoubleValue = null;
        }

        public static void UpdateNullableInt(string key, int? value)
        {
            var setting = GetOrCreate(key, SettingValueTypes.NullableInt);
            setting.RawValue = value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            setting.StringValue = null;
            setting.BoolValue = null;
            setting.IntValue = value;
            setting.DoubleValue = null;
        }

        public static void UpdateDouble(string key, double value)
        {
            var setting = GetOrCreate(key, SettingValueTypes.Double);
            setting.RawValue = value.ToString(CultureInfo.InvariantCulture);
            setting.StringValue = null;
            setting.BoolValue = null;
            setting.IntValue = null;
            setting.DoubleValue = value;
        }

        // -----------------------
        // Convenience update wrappers
        // -----------------------
        public static void UpdateDashboardActive(bool value) => UpdateBool(SettingKeys.DashboardActive, value);
        public static void UpdateDashboardScreenOrder(int value) => UpdateInt(SettingKeys.DashboardScreenOrder, value);
        public static void UpdateMainQuestActive(bool value) => UpdateBool(SettingKeys.MainQuestActive, value);
        public static void UpdateMainQuestScreenOrder(int value) => UpdateInt(SettingKeys.MainQuestScreenOrder, value);
        public static void UpdateMissionActive(bool value) => UpdateBool(SettingKeys.MissionActive, value);
        public static void UpdateMissionScreenOrder(int value) => UpdateInt(SettingKeys.MissionScreenOrder, value);
        public static void UpdateBudgetsActive(bool value) => UpdateBool(SettingKeys.BudgetsActive, value);
        public static void UpdateBudgetsScreenOrder(int value) => UpdateInt(SettingKeys.BudgetsScreenOrder, value);
        public static void UpdateAchievementsActive(bool value) => UpdateBool(SettingKeys.AchievementsActive, value);
        public static void UpdateAchievementsScreenOrder(int value) => UpdateInt(SettingKeys.AchievementsScreenOrder, value);
        public static void UpdateArcsActive(bool value) => UpdateBool(SettingKeys.ArcsActive, value);
        public static void UpdateArcsScreenOrder(int value) => UpdateInt(SettingKeys.ArcsScreenOrder, value);

        public static void UpdatePlannersActive(bool value) => UpdateBool(SettingKeys.PlannersActive, value);
        public static void UpdatePlannersScreenOrder(int value) => UpdateInt(SettingKeys.PlannersScreenOrder, value);

        public static void UpdateLocksEnabled(bool value) => UpdateBool(SettingKeys.LocksActive, value);
        public static void UpdateSchedulesEnabled(bool value) => UpdateBool(SettingKeys.SchedulesActive, value);
        public static void UpdateValueRatesEnabled(bool value) => UpdateBool(SettingKeys.ValueRatesActive, value);
        public static void UpdateCashInEnabled(bool value) => UpdateBool(SettingKeys.CashInActive, value);

        public static void UpdateHardModeEnabled(bool value) => UpdateBool(SettingKeys.HardModeEnabled, value);
        public static void UpdateHardModeDamagePerMinuteValue(double value) => UpdateDouble(SettingKeys.HardModeDamagePerMinuteValue, value);
        public static void UpdateStatusConditionsEnabled(bool value) => UpdateBool(SettingKeys.StatusConditionsEnabled, value);
        public static void UpdateCurrentlyAppliedStatusConditionId(int? value) => UpdateNullableInt(SettingKeys.CurrentlyAppliedStatusConditionId, value);
        public static void UpdateSelectedThemeId(int? value) => UpdateNullableInt(SettingKeys.SelectedThemeId, value);

        // -----------------------
        // Internals
        // -----------------------

        private static bool TryGetSetting(string key, out AcquiredSetting setting)
        {
            return _settingsByKey.TryGetValue(key, out setting!);
        }

        private static AcquiredSetting GetOrCreate(string key, string valueType)
        {
            if (_settingsByKey.TryGetValue(key, out var existing))
                return existing;

            var definition = SettingKeys.GetBuiltInSettingDefinitions()
                .FirstOrDefault(x => x.SettingKey == key);

            var created = new AcquiredSetting
            {
                SettingKey = key,
                ValueType = valueType,
                RawValue = definition?.DefaultValue ?? string.Empty,
                Category = definition?.Category ?? string.Empty,
                DisplayName = definition?.DisplayName ?? key,
                Description = definition?.Description ?? string.Empty,
                IsUserEditable = definition?.IsUserEditable ?? true,
                SortOrder = definition?.SortOrder ?? 0
            };

            _settingsByKey[key] = created;
            return created;
        }
    }


}
