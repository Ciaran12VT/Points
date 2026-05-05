using Points.Models;
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
        public const string GoalsActive = "GoalsActive";
        public const string GoalsScreenOrder = "GoalsScreenOrder";

        public const string LocksActive = "LocksActive";
        public const string SchedulesActive = "SchedulesActive";
        public const string ValueRatesActive = "ValueRatesActive";
        public const string CashInActive = "CashInActive";

        //Defaults
        public const string MissionType = "MissionType";
        public const string ValueRatesValuePerMinute = "ValueRatesValuePerMinute";
        public const string AchievementNameRegex = "AchievementNameRegex";
        public const string Username = "Username";
        public const string MissionDefaultTags = "MissionDefaultTags";
        public const string MissionDefaultSubType = "MissionDefaultSubType";
        public const string MissionDefaultValue = "MissionDefaultValue";
        public const string MissionDefaultValuePerMinute = "MissionDefaultValuePerMinute";
        public const string MissionDefaultEventDateOffsetDays = "MissionDefaultEventDateOffsetDays";
        public const string MissionDefaultEventTime = "MissionDefaultEventTime";
        public const string MissionDefaultEventIsChecked = "MissionDefaultEventIsChecked";
        public const string MissionDefaultAvailableFromDateOffsetDays = "MissionDefaultAvailableFromDateOffsetDays";
        public const string MissionDefaultAvailableFromTime = "MissionDefaultAvailableFromTime";
        public const string MissionDefaultDueByDateOffsetDays = "MissionDefaultDueByDateOffsetDays";
        public const string MissionDefaultDueByTime = "MissionDefaultDueByTime";
        public const string MissionDefaultEstimatedTime = "MissionDefaultEstimatedTime";
        public const string ReportQueryTimeoutMilliseconds = "ReportQueryTimeoutMilliseconds";

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
                    DefaultValue = "-0.2",
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
                    SettingKey = SettingKeys.GoalsActive,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.Bool,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Goals Active",
                    Description = "Whether the Goals module is enabled.",
                    IsUserEditable = true,
                    SortOrder = 130
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.GoalsScreenOrder,
                    DefaultValue = "7",
                    ValueType = SettingValueTypes.Int,
                    Category = "ModulesAndFeatures",
                    DisplayName = "Goals Screen Order",
                    Description = "Display order for the Goals module.",
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
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionType,
                    DefaultValue = "true",
                    ValueType = SettingValueTypes.String,
                    Category = "Defaults",
                    DisplayName = "Default Mission Type",
                    Description = "The default mission type assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 10
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.ValueRatesValuePerMinute,
                    DefaultValue = "1.0",
                    ValueType = SettingValueTypes.Double,
                    Category = "Defaults",
                    DisplayName = "Default Value Per Minute",
                    Description = "The default value per minute assigned when creating a new value rate.",
                    IsUserEditable = true,
                    SortOrder = 20
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.AchievementNameRegex,
                    DefaultValue = @"^(?<name>.+?)(\s*\#(?<tags>.+))?$",
                    ValueType = SettingValueTypes.String,
                    Category = "Defaults",
                    DisplayName = "Achievement Name Regex",
                    Description = "The regular expression used to parse achievement names and tags when importing from text. Must contain 'name' and 'tags' named capture groups.",
                    IsUserEditable = true,
                    SortOrder = 30
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.Username,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Username",
                    Description = "The user's display name.",
                    IsUserEditable = true,
                    SortOrder = 10
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultTags,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Tags",
                    Description = "Default tags assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 20
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultSubType,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission SubType",
                    Description = "Default subtype assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 30
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultValue,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Value",
                    Description = "Default value assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 40
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultValuePerMinute,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Value Per Minute",
                    Description = "Default value per minute assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 50
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultEventDateOffsetDays,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.NullableInt,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Event Date Offset",
                    Description = "Default event date offset in days from today when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 60
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultEventTime,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Event Time",
                    Description = "Default event time assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 70
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultEventIsChecked,
                    DefaultValue = "false",
                    ValueType = SettingValueTypes.Bool,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Event Is Checked",
                    Description = "Whether the event date checkbox is enabled by default for new missions.",
                    IsUserEditable = true,
                    SortOrder = 80
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultAvailableFromDateOffsetDays,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.NullableInt,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Available From Date Offset",
                    Description = "Default available-from date offset in days from today when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 90
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultAvailableFromTime,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Available From Time",
                    Description = "Default available-from time assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 100
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultDueByDateOffsetDays,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.NullableInt,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Due By Date Offset",
                    Description = "Default due-by date offset in days from today when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 110
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultDueByTime,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Due By Time",
                    Description = "Default due-by time assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 120
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.MissionDefaultEstimatedTime,
                    DefaultValue = "",
                    ValueType = SettingValueTypes.String,
                    Category = "DefaultsAndMisc",
                    DisplayName = "Mission Estimated Time",
                    Description = "Default estimated time assigned when creating a new mission.",
                    IsUserEditable = true,
                    SortOrder = 130
                },
                new SettingDefinition
                {
                    SettingKey = SettingKeys.ReportQueryTimeoutMilliseconds,
                    DefaultValue = "5000",
                    ValueType = SettingValueTypes.Int,
                    Category = "Database",
                    DisplayName = "Report Query Timeout",
                    Description = "Maximum time, in milliseconds, that a report query may run before it is interrupted.",
                    IsUserEditable = true,
                    SortOrder = 10
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
        public static double HardModeDamagePerMinuteValue => GetDouble(SettingKeys.HardModeDamagePerMinuteValue, -0.2d);
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

        public static bool GoalsActive => GetBool(SettingKeys.GoalsActive, true);
        public static int GoalsScreenOrder => GetInt(SettingKeys.GoalsScreenOrder, 7);

        // -----------------------
        // Feature flags
        // -----------------------

        public static bool IsLocksEnabled => GetBool(SettingKeys.LocksActive, true);
        public static bool IsSchedulesEnabled => GetBool(SettingKeys.SchedulesActive, true);
        public static bool IsValueRatesEnabled => GetBool(SettingKeys.ValueRatesActive, true);
        public static bool IsCashInEnabled => GetBool(SettingKeys.CashInActive, true);

        // -----------------------
        // Defaults
        // -----------------------

        public static string DefaultMissionType => GetString(SettingKeys.MissionType, "Stable");
        public static double DefaultValueRatesValuePerMinute => GetDouble(SettingKeys.ValueRatesValuePerMinute, 0.1);
        public static string Username => GetString(SettingKeys.Username);
        public static string DefaultMissionTags => GetString(SettingKeys.MissionDefaultTags);
        public static string DefaultMissionSubType => GetString(SettingKeys.MissionDefaultSubType);
        public static double? DefaultMissionValue => GetOptionalDouble(SettingKeys.MissionDefaultValue);
        public static double? DefaultMissionValuePerMinute => GetOptionalDouble(SettingKeys.MissionDefaultValuePerMinute);
        public static int? DefaultMissionEventDateOffsetDays => GetNullableInt(SettingKeys.MissionDefaultEventDateOffsetDays);
        public static TimeSpan? DefaultMissionEventTime => GetOptionalTime(SettingKeys.MissionDefaultEventTime);
        public static bool DefaultMissionEventIsChecked => GetBool(SettingKeys.MissionDefaultEventIsChecked, false);
        public static int? DefaultMissionAvailableFromDateOffsetDays => GetNullableInt(SettingKeys.MissionDefaultAvailableFromDateOffsetDays);
        public static TimeSpan? DefaultMissionAvailableFromTime => GetOptionalTime(SettingKeys.MissionDefaultAvailableFromTime);
        public static int? DefaultMissionDueByDateOffsetDays => GetNullableInt(SettingKeys.MissionDefaultDueByDateOffsetDays);
        public static TimeSpan? DefaultMissionDueByTime => GetOptionalTime(SettingKeys.MissionDefaultDueByTime);
        public static TimeSpan? DefaultMissionEstimatedTime => GetOptionalDuration(SettingKeys.MissionDefaultEstimatedTime);
        public static int ReportQueryTimeoutMilliseconds => GetInt(SettingKeys.ReportQueryTimeoutMilliseconds, 5000);

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

        public static double? GetOptionalDouble(string key)
        {
            var value = GetString(key);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            return null;
        }

        public static TimeSpan? GetOptionalTime(string key)
        {
            return TryParseTimeSettingValue(GetString(key), out var parsed)
                ? parsed
                : null;
        }

        public static TimeSpan? GetOptionalDuration(string key)
        {
            return TryParseDurationSettingValue(GetString(key), out var parsed)
                ? parsed
                : null;
        }

        public static void ApplyMissionDefaults(MissionCardModel model, DateTime localNow)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var tags = DefaultMissionTags;
            if (!string.IsNullOrWhiteSpace(tags))
                model.Tags = tags;

            var subTypeText = DefaultMissionSubType;
            if (!string.IsNullOrWhiteSpace(subTypeText) &&
                Enum.TryParse<MissionSubType>(subTypeText, ignoreCase: true, out var subType))
            {
                model.SubType = subType;
            }

            var value = DefaultMissionValue;
            if (value.HasValue)
                model.Value = value.Value;

            var valuePerMinute = DefaultMissionValuePerMinute;
            if (valuePerMinute.HasValue)
                model.ValuePerMinute = valuePerMinute.Value;

            var available = model.AvailableFromDate;
            if (DefaultMissionAvailableFromDateOffsetDays is int availableOffset)
                available = localNow.Date.AddDays(availableOffset) + available.TimeOfDay;

            if (DefaultMissionAvailableFromTime is TimeSpan availableTime)
                available = available.Date + availableTime;

            model.AvailableFromDate = available;

            var due = model.DueDate;
            if (DefaultMissionDueByDateOffsetDays is int dueOffset)
                due = localNow.Date.AddDays(dueOffset) + due.TimeOfDay;

            if (DefaultMissionDueByTime is TimeSpan dueTime)
                due = due.Date + dueTime;

            model.DueDate = due;

            if (DefaultMissionEventIsChecked &&
                DefaultMissionEventDateOffsetDays is int eventOffset)
            {
                var eventTime = DefaultMissionEventTime ?? TimeSpan.Zero;
                model.EventDate = localNow.Date.AddDays(eventOffset) + eventTime;
            }

            var estimatedTime = DefaultMissionEstimatedTime;
            if (estimatedTime.HasValue && estimatedTime.Value > TimeSpan.Zero)
                model.EstCompletionTime = estimatedTime.Value;
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

        public static void UpdateGoalsActive(bool value) => UpdateBool(SettingKeys.GoalsActive, value);
        public static void UpdateGoalsScreenOrder(int value) => UpdateInt(SettingKeys.GoalsScreenOrder, value);

        public static void UpdateLocksEnabled(bool value) => UpdateBool(SettingKeys.LocksActive, value);
        public static void UpdateSchedulesEnabled(bool value) => UpdateBool(SettingKeys.SchedulesActive, value);
        public static void UpdateValueRatesEnabled(bool value) => UpdateBool(SettingKeys.ValueRatesActive, value);
        public static void UpdateCashInEnabled(bool value) => UpdateBool(SettingKeys.CashInActive, value);

        public static void UpdateHardModeEnabled(bool value) => UpdateBool(SettingKeys.HardModeEnabled, value);
        public static void UpdateHardModeDamagePerMinuteValue(double value) => UpdateDouble(SettingKeys.HardModeDamagePerMinuteValue, value);
        public static void UpdateStatusConditionsEnabled(bool value) => UpdateBool(SettingKeys.StatusConditionsEnabled, value);
        public static void UpdateCurrentlyAppliedStatusConditionId(int? value) => UpdateNullableInt(SettingKeys.CurrentlyAppliedStatusConditionId, value);
        public static void UpdateSelectedThemeId(int? value) => UpdateNullableInt(SettingKeys.SelectedThemeId, value);
        public static void UpdateUsername(string value) => UpdateString(SettingKeys.Username, value);
        public static void UpdateMissionDefaultTags(string value) => UpdateString(SettingKeys.MissionDefaultTags, value);
        public static void UpdateMissionDefaultSubType(string value) => UpdateString(SettingKeys.MissionDefaultSubType, value);
        public static void UpdateMissionDefaultValue(string value) => UpdateString(SettingKeys.MissionDefaultValue, value);
        public static void UpdateMissionDefaultValuePerMinute(string value) => UpdateString(SettingKeys.MissionDefaultValuePerMinute, value);
        public static void UpdateMissionDefaultEventDateOffsetDays(int? value) => UpdateNullableInt(SettingKeys.MissionDefaultEventDateOffsetDays, value);
        public static void UpdateMissionDefaultEventTime(string value) => UpdateString(SettingKeys.MissionDefaultEventTime, value);
        public static void UpdateMissionDefaultEventIsChecked(bool value) => UpdateBool(SettingKeys.MissionDefaultEventIsChecked, value);
        public static void UpdateMissionDefaultAvailableFromDateOffsetDays(int? value) => UpdateNullableInt(SettingKeys.MissionDefaultAvailableFromDateOffsetDays, value);
        public static void UpdateMissionDefaultAvailableFromTime(string value) => UpdateString(SettingKeys.MissionDefaultAvailableFromTime, value);
        public static void UpdateMissionDefaultDueByDateOffsetDays(int? value) => UpdateNullableInt(SettingKeys.MissionDefaultDueByDateOffsetDays, value);
        public static void UpdateMissionDefaultDueByTime(string value) => UpdateString(SettingKeys.MissionDefaultDueByTime, value);
        public static void UpdateMissionDefaultEstimatedTime(string value) => UpdateString(SettingKeys.MissionDefaultEstimatedTime, value);
        public static void UpdateReportQueryTimeoutMilliseconds(int value) => UpdateInt(SettingKeys.ReportQueryTimeoutMilliseconds, value);

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

        private static bool TryParseTimeSettingValue(string? value, out TimeSpan parsed)
        {
            parsed = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Trim().Split(':');
            if (parts.Length is 2 or 3 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
            {
                var seconds = 0;
                if (parts.Length == 3 &&
                    !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                {
                    return false;
                }

                if (hours is >= 0 and <= 23 &&
                    minutes is >= 0 and <= 59 &&
                    seconds is >= 0 and <= 59)
                {
                    parsed = new TimeSpan(hours, minutes, seconds);
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseDurationSettingValue(string? value, out TimeSpan parsed)
        {
            parsed = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Trim().Split(':');
            if (parts.Length is 2 or 3 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
            {
                var seconds = 0;
                if (parts.Length == 3 &&
                    !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                {
                    return false;
                }

                if (hours >= 0 &&
                    minutes is >= 0 and <= 59 &&
                    seconds is >= 0 and <= 59)
                {
                    parsed = new TimeSpan(hours, minutes, seconds);
                    return true;
                }
            }

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out parsed) &&
                parsed >= TimeSpan.Zero)
            {
                return true;
            }

            parsed = TimeSpan.Zero;
            return false;
        }
    }


}
