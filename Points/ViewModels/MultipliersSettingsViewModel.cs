using Points.Global;
using Points.Services.Sqlite.Interfaces;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class MultipliersSettingsViewModel : Models.ObservableObject, INotifyPropertyChanged
    {
        private readonly IDbService _db;
        private readonly Func<Task>? _onSaved;

        public MultipliersSettingsViewModel(IDbService db, Func<Task>? onSaved = null)
        {
            _db = db;
            _onSaved = onSaved;

            SaveCommand = new Command(async () => await SaveAsync());

            _ = InitializeAsync();
        }

        public ICommand SaveCommand { get; }

        private bool _hardModeEnabled;
        public bool HardModeEnabled
        {
            get => _hardModeEnabled;
            set
            {
                if (SetProperty(ref _hardModeEnabled, value))
                {
                    RaisePropertyChanged(nameof(IsHardModePenaltyValid));
                    RaisePropertyChanged(nameof(HardModeIdlePenaltyPerMinute));
                }
            }
        }

        private string _hardModeIdlePenaltyText = "-0.2";
        public string HardModeIdlePenaltyText
        {
            get => _hardModeIdlePenaltyText;
            set
            {
                if (SetProperty(ref _hardModeIdlePenaltyText, value))
                {
                    RaisePropertyChanged(nameof(IsHardModePenaltyValid));
                    RaisePropertyChanged(nameof(HardModeIdlePenaltyPerMinute));
                }
            }
        }

        public double HardModeIdlePenaltyPerMinute
        {
            get
            {
                if (!double.TryParse(_hardModeIdlePenaltyText, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v)
                    && !double.TryParse(_hardModeIdlePenaltyText, out v))
                {
                    return 0.0;
                }

                return -Math.Abs(v);
            }
        }

        public bool IsHardModePenaltyValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_hardModeIdlePenaltyText))
                    return false;

                var parsed =
                    double.TryParse(_hardModeIdlePenaltyText, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var invariantValue)
                    || double.TryParse(_hardModeIdlePenaltyText, out invariantValue);

                return parsed && Math.Abs(invariantValue) > 0.0000001;
            }
        }

        private async Task InitializeAsync()
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var settings = await _db.GetSettingsAsync();

            var hardModeEnabledSetting = settings.FirstOrDefault(x => x.SettingKey == SettingKeys.HardModeEnabled);
            HardModeEnabled = hardModeEnabledSetting?.BoolValue ?? false;

            var hardModePenaltySetting = settings.FirstOrDefault(x => x.SettingKey == SettingKeys.HardModeDamagePerMinuteValue);
            var storedPenalty = hardModePenaltySetting?.DoubleValue ?? -0.2;

            HardModeIdlePenaltyText = storedPenalty.ToString(CultureInfo.InvariantCulture);
        }

        private async Task SaveAsync()
        {
            await _db.SetBoolSettingAsync(SettingKeys.HardModeEnabled, HardModeEnabled);
            await _db.SetDoubleSettingAsync(SettingKeys.HardModeDamagePerMinuteValue, HardModeIdlePenaltyPerMinute);

            SettingsProvider.UpdateHardModeEnabled(HardModeEnabled);
            SettingsProvider.UpdateHardModeDamagePerMinuteValue(HardModeIdlePenaltyPerMinute);

            if (_onSaved != null) await _onSaved();
        }
    }
}