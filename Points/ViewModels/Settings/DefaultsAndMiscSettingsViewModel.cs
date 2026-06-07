using Points.Global;
using Points.Models;
using Points.Services.Persistence;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace Points.ViewModels.Settings
{
    public class DefaultsAndMiscSettingsViewModel : ObservableObject, INotifyPropertyChanged
    {
        private const string UseAppDefaultSubTypeOption = "Use app default";

        private readonly ISettingsService _settings;
        private readonly Func<Task>? _onSaved;

        public DefaultsAndMiscSettingsViewModel(ISettingsService settings, Func<Task>? onSaved = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _onSaved = onSaved;

            MissionSubTypeOptions = new ObservableCollection<string>(
                new[] { UseAppDefaultSubTypeOption }
                    .Concat(Enum.GetNames<MissionSubType>()));

            SelectedMissionSubType = UseAppDefaultSubTypeOption;
            SaveCommand = new Command(async () => await SaveAsync());

            _ = InitializeAsync();
        }

        public ICommand SaveCommand { get; }
        public ObservableCollection<string> MissionSubTypeOptions { get; }

        private string _usernameText = "";
        public string UsernameText
        {
            get => _usernameText;
            set => SetProperty(ref _usernameText, value);
        }

        private string _missionTagsText = "";
        public string MissionTagsText
        {
            get => _missionTagsText;
            set => SetProperty(ref _missionTagsText, value);
        }

        private string _selectedMissionSubType = "";
        public string SelectedMissionSubType
        {
            get => _selectedMissionSubType;
            set => SetProperty(ref _selectedMissionSubType, value);
        }

        private string _missionValueText = "";
        public string MissionValueText
        {
            get => _missionValueText;
            set
            {
                if (SetProperty(ref _missionValueText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private string _missionValuePerMinuteText = "";
        public string MissionValuePerMinuteText
        {
            get => _missionValuePerMinuteText;
            set
            {
                if (SetProperty(ref _missionValuePerMinuteText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private string _eventDateOffsetDaysText = "";
        public string EventDateOffsetDaysText
        {
            get => _eventDateOffsetDaysText;
            set
            {
                if (SetProperty(ref _eventDateOffsetDaysText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private string _eventTimeText = "";
        public string EventTimeText
        {
            get => _eventTimeText;
            set
            {
                if (SetProperty(ref _eventTimeText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private bool _eventIsChecked;
        public bool EventIsChecked
        {
            get => _eventIsChecked;
            set => SetProperty(ref _eventIsChecked, value);
        }

        private string _availableFromDateOffsetDaysText = "";
        public string AvailableFromDateOffsetDaysText
        {
            get => _availableFromDateOffsetDaysText;
            set
            {
                if (SetProperty(ref _availableFromDateOffsetDaysText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private string _availableFromTimeText = "";
        public string AvailableFromTimeText
        {
            get => _availableFromTimeText;
            set
            {
                if (SetProperty(ref _availableFromTimeText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private string _dueByDateOffsetDaysText = "";
        public string DueByDateOffsetDaysText
        {
            get => _dueByDateOffsetDaysText;
            set
            {
                if (SetProperty(ref _dueByDateOffsetDaysText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private string _dueByTimeText = "";
        public string DueByTimeText
        {
            get => _dueByTimeText;
            set
            {
                if (SetProperty(ref _dueByTimeText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        private string _estimatedTimeText = "";
        public string EstimatedTimeText
        {
            get => _estimatedTimeText;
            set
            {
                if (SetProperty(ref _estimatedTimeText, value))
                    RaiseValidationPropertiesChanged();
            }
        }

        public bool HasInvalidNumbers =>
            !IsOptionalDouble(MissionValueText) ||
            !IsOptionalDouble(MissionValuePerMinuteText);

        public bool HasInvalidDateOffsets =>
            !IsOptionalInt(EventDateOffsetDaysText) ||
            !IsOptionalInt(AvailableFromDateOffsetDaysText) ||
            !IsOptionalInt(DueByDateOffsetDaysText);

        public bool HasInvalidTimes =>
            !IsOptionalClockTime(EventTimeText) ||
            !IsOptionalClockTime(AvailableFromTimeText) ||
            !IsOptionalClockTime(DueByTimeText);

        public bool HasInvalidEstimatedTime =>
            !IsOptionalDuration(EstimatedTimeText);

        public bool HasValidationErrors =>
            HasInvalidNumbers ||
            HasInvalidDateOffsets ||
            HasInvalidTimes ||
            HasInvalidEstimatedTime;

        private async Task InitializeAsync()
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var settings = await _settings.GetSettingsAsync();

            UsernameText = GetString(settings, SettingKeys.Username);
            MissionTagsText = GetString(settings, SettingKeys.MissionDefaultTags);

            var subType = GetString(settings, SettingKeys.MissionDefaultSubType);
            SelectedMissionSubType = MissionSubTypeOptions.Contains(subType)
                ? subType
                : UseAppDefaultSubTypeOption;

            MissionValueText = GetString(settings, SettingKeys.MissionDefaultValue);
            MissionValuePerMinuteText = GetString(settings, SettingKeys.MissionDefaultValuePerMinute);
            EventDateOffsetDaysText = FormatNullableInt(GetNullableInt(settings, SettingKeys.MissionDefaultEventDateOffsetDays));
            EventTimeText = GetString(settings, SettingKeys.MissionDefaultEventTime);
            EventIsChecked = GetBool(settings, SettingKeys.MissionDefaultEventIsChecked, false);
            AvailableFromDateOffsetDaysText = FormatNullableInt(GetNullableInt(settings, SettingKeys.MissionDefaultAvailableFromDateOffsetDays));
            AvailableFromTimeText = GetString(settings, SettingKeys.MissionDefaultAvailableFromTime);
            DueByDateOffsetDaysText = FormatNullableInt(GetNullableInt(settings, SettingKeys.MissionDefaultDueByDateOffsetDays));
            DueByTimeText = GetString(settings, SettingKeys.MissionDefaultDueByTime);
            EstimatedTimeText = GetString(settings, SettingKeys.MissionDefaultEstimatedTime);
        }

        private async Task SaveAsync()
        {
            if (HasValidationErrors)
                return;

            var subType = SelectedMissionSubType == UseAppDefaultSubTypeOption
                ? ""
                : SelectedMissionSubType;

            await _settings.SetStringSettingAsync(SettingKeys.Username, UsernameText.Trim());
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultTags, MissionTagsText.Trim());
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultSubType, subType);
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultValue, MissionValueText.Trim());
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultValuePerMinute, MissionValuePerMinuteText.Trim());
            await _settings.SetNullableIntSettingAsync(SettingKeys.MissionDefaultEventDateOffsetDays, ParseNullableInt(EventDateOffsetDaysText));
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultEventTime, EventTimeText.Trim());
            await _settings.SetBoolSettingAsync(SettingKeys.MissionDefaultEventIsChecked, EventIsChecked);
            await _settings.SetNullableIntSettingAsync(SettingKeys.MissionDefaultAvailableFromDateOffsetDays, ParseNullableInt(AvailableFromDateOffsetDaysText));
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultAvailableFromTime, AvailableFromTimeText.Trim());
            await _settings.SetNullableIntSettingAsync(SettingKeys.MissionDefaultDueByDateOffsetDays, ParseNullableInt(DueByDateOffsetDaysText));
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultDueByTime, DueByTimeText.Trim());
            await _settings.SetStringSettingAsync(SettingKeys.MissionDefaultEstimatedTime, EstimatedTimeText.Trim());

            SettingsProvider.UpdateUsername(UsernameText.Trim());
            SettingsProvider.UpdateMissionDefaultTags(MissionTagsText.Trim());
            SettingsProvider.UpdateMissionDefaultSubType(subType);
            SettingsProvider.UpdateMissionDefaultValue(MissionValueText.Trim());
            SettingsProvider.UpdateMissionDefaultValuePerMinute(MissionValuePerMinuteText.Trim());
            SettingsProvider.UpdateMissionDefaultEventDateOffsetDays(ParseNullableInt(EventDateOffsetDaysText));
            SettingsProvider.UpdateMissionDefaultEventTime(EventTimeText.Trim());
            SettingsProvider.UpdateMissionDefaultEventIsChecked(EventIsChecked);
            SettingsProvider.UpdateMissionDefaultAvailableFromDateOffsetDays(ParseNullableInt(AvailableFromDateOffsetDaysText));
            SettingsProvider.UpdateMissionDefaultAvailableFromTime(AvailableFromTimeText.Trim());
            SettingsProvider.UpdateMissionDefaultDueByDateOffsetDays(ParseNullableInt(DueByDateOffsetDaysText));
            SettingsProvider.UpdateMissionDefaultDueByTime(DueByTimeText.Trim());
            SettingsProvider.UpdateMissionDefaultEstimatedTime(EstimatedTimeText.Trim());

            if (_onSaved != null) await _onSaved();
        }

        private void RaiseValidationPropertiesChanged()
        {
            RaisePropertyChanged(nameof(HasInvalidNumbers));
            RaisePropertyChanged(nameof(HasInvalidDateOffsets));
            RaisePropertyChanged(nameof(HasInvalidTimes));
            RaisePropertyChanged(nameof(HasInvalidEstimatedTime));
            RaisePropertyChanged(nameof(HasValidationErrors));
        }

        private static string GetString(List<AcquiredSetting> settings, string key)
        {
            var setting = settings.FirstOrDefault(x => x.SettingKey == key);
            return setting?.StringValue ?? setting?.RawValue ?? "";
        }

        private static bool GetBool(List<AcquiredSetting> settings, string key, bool defaultValue)
        {
            return settings.FirstOrDefault(x => x.SettingKey == key)?.BoolValue ?? defaultValue;
        }

        private static int? GetNullableInt(List<AcquiredSetting> settings, string key)
        {
            return settings.FirstOrDefault(x => x.SettingKey == key)?.IntValue;
        }

        private static string FormatNullableInt(int? value)
        {
            return value?.ToString(CultureInfo.InvariantCulture) ?? "";
        }

        private static bool IsOptionalInt(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        private static int? ParseNullableInt(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static bool IsOptionalDouble(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _);
        }

        private static bool IsOptionalClockTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            return TryParseClockTime(value, out _);
        }

        private static bool IsOptionalDuration(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            return TryParseDuration(value, out _);
        }

        private static bool TryParseClockTime(string value, out TimeSpan parsed)
        {
            parsed = TimeSpan.Zero;

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

        private static bool TryParseDuration(string value, out TimeSpan parsed)
        {
            parsed = TimeSpan.Zero;

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
