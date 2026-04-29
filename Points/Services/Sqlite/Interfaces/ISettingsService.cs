using Points.Global;

namespace Points.Services.Sqlite.Interfaces
{
    public interface ISettingsService
    {
        Task<List<AcquiredSetting>> GetSettingsAsync();

        Task SetStringSettingAsync(string settingKey, string value);
        Task SetBoolSettingAsync(string settingKey, bool value);
        Task SetIntSettingAsync(string settingKey, int value);
        Task SetNullableIntSettingAsync(string settingKey, int? value);
        Task SetDoubleSettingAsync(string settingKey, double value);
    }
}
