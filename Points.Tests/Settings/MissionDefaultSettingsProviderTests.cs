using Points.Global;
using Points.Models;
using Points.Models.DbModels;
using Xunit;

namespace Points.Tests.Settings;

public sealed class MissionDefaultSettingsProviderTests
{
    [Fact]
    public void ApplyMissionDefaults_AppliesConfiguredMissionDefaults()
    {
        SettingsProvider.Initialize(new List<AcquiredSetting>
        {
            StringSetting(SettingKeys.MissionDefaultTags, "#work, #focus"),
            StringSetting(SettingKeys.MissionDefaultSubType, "Rot"),
            StringSetting(SettingKeys.MissionDefaultValue, "42.5"),
            StringSetting(SettingKeys.MissionDefaultValuePerMinute, "0.75"),
            NullableIntSetting(SettingKeys.MissionDefaultEventDateOffsetDays, 3),
            StringSetting(SettingKeys.MissionDefaultEventTime, "14:30"),
            BoolSetting(SettingKeys.MissionDefaultEventIsChecked, true),
            NullableIntSetting(SettingKeys.MissionDefaultAvailableFromDateOffsetDays, 0),
            StringSetting(SettingKeys.MissionDefaultAvailableFromTime, "08:15"),
            NullableIntSetting(SettingKeys.MissionDefaultDueByDateOffsetDays, 5),
            StringSetting(SettingKeys.MissionDefaultDueByTime, "17:45"),
            StringSetting(SettingKeys.MissionDefaultEstimatedTime, "26:05:06")
        });

        var localNow = new DateTime(2026, 5, 4, 10, 20, 30);
        var mission = new MissionCardModel
        {
            Tags = "",
            SubType = MissionSubType.Stable,
            Value = 0,
            ValuePerMinute = 0,
            AvailableFromDate = localNow,
            DueDate = localNow.AddDays(1)
        };

        SettingsProvider.ApplyMissionDefaults(mission, localNow);

        Assert.Equal("#work, #focus", mission.Tags);
        Assert.Equal(MissionSubType.Rot, mission.SubType);
        Assert.Equal(42.5, mission.Value);
        Assert.Equal(0.75, mission.ValuePerMinute);
        Assert.Equal(new DateTime(2026, 5, 4, 8, 15, 0), mission.AvailableFromDate);
        Assert.Equal(new DateTime(2026, 5, 9, 17, 45, 0), mission.DueDate);
        Assert.Equal(new DateTime(2026, 5, 7, 14, 30, 0), mission.EventDate);
        Assert.Equal(new TimeSpan(26, 5, 6), mission.EstCompletionTime);
    }

    [Fact]
    public void ApplyMissionDefaults_IgnoresBlankOrInvalidOptionalValues()
    {
        SettingsProvider.Initialize(new List<AcquiredSetting>
        {
            StringSetting(SettingKeys.MissionDefaultTags, ""),
            StringSetting(SettingKeys.MissionDefaultSubType, "Unknown"),
            StringSetting(SettingKeys.MissionDefaultValue, "not-a-number"),
            StringSetting(SettingKeys.MissionDefaultValuePerMinute, ""),
            StringSetting(SettingKeys.MissionDefaultEventTime, "99:99"),
            BoolSetting(SettingKeys.MissionDefaultEventIsChecked, true),
            StringSetting(SettingKeys.MissionDefaultAvailableFromTime, "invalid"),
            StringSetting(SettingKeys.MissionDefaultDueByTime, ""),
            StringSetting(SettingKeys.MissionDefaultEstimatedTime, "1:70:00")
        });

        var localNow = new DateTime(2026, 5, 4, 10, 20, 30);
        var mission = new MissionCardModel
        {
            Tags = "existing",
            SubType = MissionSubType.Degrade,
            Value = 7,
            ValuePerMinute = 0.25,
            AvailableFromDate = localNow,
            DueDate = localNow.AddDays(1)
        };

        SettingsProvider.ApplyMissionDefaults(mission, localNow);

        Assert.Equal("existing", mission.Tags);
        Assert.Equal(MissionSubType.Degrade, mission.SubType);
        Assert.Equal(7, mission.Value);
        Assert.Equal(0.25, mission.ValuePerMinute);
        Assert.Equal(localNow, mission.AvailableFromDate);
        Assert.Equal(localNow.AddDays(1), mission.DueDate);
        Assert.Null(mission.EventDate);
        Assert.Null(mission.EstCompletionTime);
    }

    private static AcquiredSetting StringSetting(string key, string value)
    {
        return new AcquiredSetting
        {
            SettingKey = key,
            ValueType = SettingValueTypes.String,
            RawValue = value,
            StringValue = value
        };
    }

    private static AcquiredSetting BoolSetting(string key, bool value)
    {
        return new AcquiredSetting
        {
            SettingKey = key,
            ValueType = SettingValueTypes.Bool,
            RawValue = value ? "true" : "false",
            BoolValue = value
        };
    }

    private static AcquiredSetting NullableIntSetting(string key, int? value)
    {
        return new AcquiredSetting
        {
            SettingKey = key,
            ValueType = SettingValueTypes.NullableInt,
            RawValue = value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            IntValue = value
        };
    }
}
