using Points.Global;
using Points.Services.Persistence;
using Points.Services.Time;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace Points.ViewModels.Settings
{
    public class MultipliersSettingsViewModel : Models.ObservableObject, INotifyPropertyChanged
    {
        private readonly ISettingsService _settings;
        private readonly IHardModePenaltyService _hardModePenalties;
        private readonly IClock _clock;
        private readonly Func<Task>? _onSaved;
        private readonly Command _saveCommand;

        public MultipliersSettingsViewModel(
            ISettingsService settings,
            IHardModePenaltyService hardModePenalties,
            IClock clock,
            Func<Task>? onSaved = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _hardModePenalties = hardModePenalties ?? throw new ArgumentNullException(nameof(hardModePenalties));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _onSaved = onSaved;

            _saveCommand = new Command(async () => await SaveAsync(), () => CanSave);
            SaveCommand = _saveCommand;

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
                    RaisePropertyChanged(nameof(CanSave));
                    _saveCommand.ChangeCanExecute();
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
                    RaisePropertyChanged(nameof(CanSave));
                    _saveCommand.ChangeCanExecute();
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

        public bool CanSave => !HardModeEnabled || IsHardModePenaltyValid;

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
            var settings = await _settings.GetSettingsAsync();

            var hardModeEnabledSetting = settings.FirstOrDefault(x => x.SettingKey == SettingKeys.HardModeEnabled);
            HardModeEnabled = hardModeEnabledSetting?.BoolValue ?? false;

            var hardModePenaltySetting = settings.FirstOrDefault(x => x.SettingKey == SettingKeys.HardModeDamagePerMinuteValue);
            var storedPenalty = hardModePenaltySetting?.DoubleValue ?? -0.2;

            HardModeIdlePenaltyText = storedPenalty.ToString(CultureInfo.InvariantCulture);
        }

        private async Task SaveAsync()
        {
            if (!CanSave)
                return;

            await _settings.SetBoolSettingAsync(SettingKeys.HardModeEnabled, HardModeEnabled);
            await _settings.SetDoubleSettingAsync(SettingKeys.HardModeDamagePerMinuteValue, HardModeIdlePenaltyPerMinute);

            SettingsProvider.UpdateHardModeEnabled(HardModeEnabled);
            SettingsProvider.UpdateHardModeDamagePerMinuteValue(HardModeIdlePenaltyPerMinute);

            await _hardModePenalties.ReconcileAsync(_clock.UtcNow);

            if (_onSaved != null) await _onSaved();
        }
    }
}
