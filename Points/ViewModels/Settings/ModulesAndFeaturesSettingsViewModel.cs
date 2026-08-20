using CommunityToolkit.Mvvm.Input;
using Points.Global;
using Points.Services.Notifications;
using Points.Services.Persistence;
using System.ComponentModel;

namespace Points.ViewModels.Settings
{
    public class ModulesAndFeaturesSettingsViewModel : Models.ObservableObject, INotifyPropertyChanged
    {
        private readonly ISettingsService _settings;
        private readonly IActiveCardNotificationAvailabilityService _notificationAvailability;
        private readonly Func<Task>? _reconcileNotificationAsync;
        private readonly Func<Task>? _onSaved;
        private readonly AsyncRelayCommand _saveCommand;
        private readonly AsyncRelayCommand _openNotificationSettingsCommand;

        public ModulesAndFeaturesSettingsViewModel(
            ISettingsService settings,
            IActiveCardNotificationAvailabilityService notificationAvailability,
            Func<Task>? reconcileNotificationAsync = null,
            Func<Task>? onSaved = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _notificationAvailability = notificationAvailability
                ?? throw new ArgumentNullException(nameof(notificationAvailability));
            _reconcileNotificationAsync = reconcileNotificationAsync;
            _onSaved = onSaved;
            _saveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            _openNotificationSettingsCommand = new AsyncRelayCommand(
                OpenNotificationSettingsAsync,
                () => CanOpenNotificationSettings);

            Initialization = InitializeAsync();
        }

        public IAsyncRelayCommand SaveCommand => _saveCommand;
        public IAsyncRelayCommand OpenNotificationSettingsCommand => _openNotificationSettingsCommand;
        public Task Initialization { get; }

        private bool _dashboardActive;
        public bool DashboardActive
        {
            get => _dashboardActive;
            set => SetProperty(ref _dashboardActive, value);
        }

        private string _dashboardScreenOrderText = "1";
        public string DashboardScreenOrderText
        {
            get => _dashboardScreenOrderText;
            set
            {
                if (SetProperty(ref _dashboardScreenOrderText, value))
                {
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
                    NotifySaveStateChanged();
                }
            }
        }

        private bool _mainQuestActive;
        public bool MainQuestActive
        {
            get => _mainQuestActive;
            set => SetProperty(ref _mainQuestActive, value);
        }

        private string _mainQuestScreenOrderText = "2";
        public string MainQuestScreenOrderText
        {
            get => _mainQuestScreenOrderText;
            set
            {
                if (SetProperty(ref _mainQuestScreenOrderText, value))
                {
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
                    NotifySaveStateChanged();
                }
            }
        }

        private bool _missionActive;
        public bool MissionActive
        {
            get => _missionActive;
            set => SetProperty(ref _missionActive, value);
        }

        private string _missionScreenOrderText = "3";
        public string MissionScreenOrderText
        {
            get => _missionScreenOrderText;
            set
            {
                if (SetProperty(ref _missionScreenOrderText, value))
                {
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
                    NotifySaveStateChanged();
                }
            }
        }

        private bool _budgetsActive;
        public bool BudgetsActive
        {
            get => _budgetsActive;
            set => SetProperty(ref _budgetsActive, value);
        }

        private string _budgetsScreenOrderText = "4";
        public string BudgetsScreenOrderText
        {
            get => _budgetsScreenOrderText;
            set
            {
                if (SetProperty(ref _budgetsScreenOrderText, value))
                {
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
                    NotifySaveStateChanged();
                }
            }
        }

        private bool _achievementsActive;
        public bool AchievementsActive
        {
            get => _achievementsActive;
            set => SetProperty(ref _achievementsActive, value);
        }

        private string _achievementsScreenOrderText = "5";
        public string AchievementsScreenOrderText
        {
            get => _achievementsScreenOrderText;
            set
            {
                if (SetProperty(ref _achievementsScreenOrderText, value))
                {
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
                    NotifySaveStateChanged();
                }
            }
        }

        private bool _arcsActive;
        public bool ArcsActive
        {
            get => _arcsActive;
            set => SetProperty(ref _arcsActive, value);
        }

        private string _arcsScreenOrderText = "6";
        public string ArcsScreenOrderText
        {
            get => _arcsScreenOrderText;
            set
            {
                if (SetProperty(ref _arcsScreenOrderText, value))
                {
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
                    NotifySaveStateChanged();
                }
            }
        }

        private bool _goalsActive;
        public bool GoalsActive
        {
            get => _goalsActive;
            set => SetProperty(ref _goalsActive, value);
        }

        private string _goalsScreenOrderText = "7";
        public string GoalsScreenOrderText
        {
            get => _goalsScreenOrderText;
            set
            {
                if (SetProperty(ref _goalsScreenOrderText, value))
                {
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
                    NotifySaveStateChanged();
                }
            }
        }

        private bool _locksActive;
        public bool LocksActive
        {
            get => _locksActive;
            set => SetProperty(ref _locksActive, value);
        }

        private bool _schedulesActive;
        public bool SchedulesActive
        {
            get => _schedulesActive;
            set => SetProperty(ref _schedulesActive, value);
        }

        private bool _valueRatesActive;
        public bool ValueRatesActive
        {
            get => _valueRatesActive;
            set => SetProperty(ref _valueRatesActive, value);
        }

        private bool _cashInActive;
        public bool CashInActive
        {
            get => _cashInActive;
            set => SetProperty(ref _cashInActive, value);
        }

        private bool _deadAirNotificationEnabled;
        public bool DeadAirNotificationEnabled
        {
            get => _deadAirNotificationEnabled;
            set
            {
                if (!SetProperty(ref _deadAirNotificationEnabled, value))
                    return;

                if (!value)
                    SetDeadAirAlertNoiseEnabled(false, allowUnavailableEnable: false);

                NotifyDeadAirAlertStateChanged();
            }
        }

        private bool _deadAirAlertNoiseEnabled;
        public bool DeadAirAlertNoiseEnabled
        {
            get => _deadAirAlertNoiseEnabled;
            set => SetDeadAirAlertNoiseEnabled(value, allowUnavailableEnable: false);
        }

        private ActiveCardNotificationAvailability _activeCardNotificationAvailability =
            ActiveCardNotificationAvailability.Unknown;

        public bool IsActiveCardNotificationAvailable =>
            _activeCardNotificationAvailability.IsAvailable;

        public bool CanChangeDeadAirAlertNoise =>
            DeadAirNotificationEnabled &&
            (IsActiveCardNotificationAvailable || DeadAirAlertNoiseEnabled);

        public bool IsDeadAirAlertAvailabilityWarningVisible =>
            DeadAirNotificationEnabled && !IsActiveCardNotificationAvailable;

        public bool CanOpenNotificationSettings =>
            IsDeadAirAlertAvailabilityWarningVisible &&
            _activeCardNotificationAvailability.CanOpenSettings;

        public string DeadAirAlertAvailabilityMessage
        {
            get
            {
                var prefix = DeadAirAlertNoiseEnabled ? "Paused: " : "Unavailable: ";
                var detail = _activeCardNotificationAvailability.Status switch
                {
                    ActiveCardNotificationAvailabilityStatus.PermissionDenied =>
                        "allow notification permission for Points to use Dead Air alert noise.",
                    ActiveCardNotificationAvailabilityStatus.AppNotificationsDisabled =>
                        "enable notifications for Points to use Dead Air alert noise.",
                    ActiveCardNotificationAvailabilityStatus.ChannelDisabled =>
                        "enable the Active card notification channel to use Dead Air alert noise.",
                    ActiveCardNotificationAvailabilityStatus.UnsupportedPlatform =>
                        "Dead Air alert noise is available on Android only.",
                    _ => "notification access could not be verified."
                };

                return prefix + detail;
            }
        }

        private bool _isInitialized;
        public bool IsInitialized
        {
            get => _isInitialized;
            private set
            {
                if (SetProperty(ref _isInitialized, value))
                    NotifySaveStateChanged();
            }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                    NotifySaveStateChanged();
            }
        }

        public bool CanSave => IsInitialized && !IsSaving && !HasInvalidScreenOrder;

        public bool HasInvalidScreenOrder =>
            !IsValidInt(DashboardScreenOrderText) ||
            !IsValidInt(MainQuestScreenOrderText) ||
            !IsValidInt(MissionScreenOrderText) ||
            !IsValidInt(BudgetsScreenOrderText) ||
            !IsValidInt(AchievementsScreenOrderText) ||
            !IsValidInt(ArcsScreenOrderText) ||
            !IsValidInt(GoalsScreenOrderText);

        private async Task InitializeAsync()
        {
            await LoadAsync();
            await RefreshNotificationAvailabilityAsync();
            IsInitialized = true;
        }

        private async Task LoadAsync()
        {
            var settings = await _settings.GetSettingsAsync();

            DashboardActive = GetBool(settings, SettingKeys.DashboardActive, true);
            DashboardScreenOrderText = GetInt(settings, SettingKeys.DashboardScreenOrder, 1).ToString();

            MainQuestActive = GetBool(settings, SettingKeys.MainQuestActive, true);
            MainQuestScreenOrderText = GetInt(settings, SettingKeys.MainQuestScreenOrder, 2).ToString();

            MissionActive = GetBool(settings, SettingKeys.MissionActive, true);
            MissionScreenOrderText = GetInt(settings, SettingKeys.MissionScreenOrder, 3).ToString();

            BudgetsActive = GetBool(settings, SettingKeys.BudgetsActive, true);
            BudgetsScreenOrderText = GetInt(settings, SettingKeys.BudgetsScreenOrder, 4).ToString();

            AchievementsActive = GetBool(settings, SettingKeys.AchievementsActive, true);
            AchievementsScreenOrderText = GetInt(settings, SettingKeys.AchievementsScreenOrder, 5).ToString();

            ArcsActive = GetBool(settings, SettingKeys.ArcsActive, true);
            ArcsScreenOrderText = GetInt(settings, SettingKeys.ArcsScreenOrder, 6).ToString();

            GoalsActive = GetBool(settings, SettingKeys.GoalsActive, true);
            GoalsScreenOrderText = GetInt(settings, SettingKeys.GoalsScreenOrder, 7).ToString();

            LocksActive = GetBool(settings, SettingKeys.LocksActive, true);
            SchedulesActive = GetBool(settings, SettingKeys.SchedulesActive, true);
            ValueRatesActive = GetBool(settings, SettingKeys.ValueRatesActive, true);
            CashInActive = GetBool(settings, SettingKeys.CashInActive, true);
            DeadAirNotificationEnabled = GetBool(settings, SettingKeys.DeadAirNotificationEnabled, false);
            SetDeadAirAlertNoiseEnabled(
                GetBool(settings, SettingKeys.DeadAirAlertNoiseEnabled, false),
                allowUnavailableEnable: true);
        }

        private async Task SaveAsync()
        {
            if (!CanSave)
                return;

            IsSaving = true;

            try
            {
                var normalizedDeadAirAlertNoise =
                    DeadAirNotificationEnabled && DeadAirAlertNoiseEnabled;

                await _settings.SetBoolSettingAsync(SettingKeys.DashboardActive, DashboardActive);
                await _settings.SetIntSettingAsync(SettingKeys.DashboardScreenOrder, ParseInt(DashboardScreenOrderText, 1));

                await _settings.SetBoolSettingAsync(SettingKeys.MainQuestActive, MainQuestActive);
                await _settings.SetIntSettingAsync(SettingKeys.MainQuestScreenOrder, ParseInt(MainQuestScreenOrderText, 2));

                await _settings.SetBoolSettingAsync(SettingKeys.MissionActive, MissionActive);
                await _settings.SetIntSettingAsync(SettingKeys.MissionScreenOrder, ParseInt(MissionScreenOrderText, 3));

                await _settings.SetBoolSettingAsync(SettingKeys.BudgetsActive, BudgetsActive);
                await _settings.SetIntSettingAsync(SettingKeys.BudgetsScreenOrder, ParseInt(BudgetsScreenOrderText, 4));

                await _settings.SetBoolSettingAsync(SettingKeys.AchievementsActive, AchievementsActive);
                await _settings.SetIntSettingAsync(SettingKeys.AchievementsScreenOrder, ParseInt(AchievementsScreenOrderText, 5));

                await _settings.SetBoolSettingAsync(SettingKeys.ArcsActive, ArcsActive);
                await _settings.SetIntSettingAsync(SettingKeys.ArcsScreenOrder, ParseInt(ArcsScreenOrderText, 6));

                await _settings.SetBoolSettingAsync(SettingKeys.GoalsActive, GoalsActive);
                await _settings.SetIntSettingAsync(SettingKeys.GoalsScreenOrder, ParseInt(GoalsScreenOrderText, 7));

                await _settings.SetBoolSettingAsync(SettingKeys.LocksActive, LocksActive);
                await _settings.SetBoolSettingAsync(SettingKeys.SchedulesActive, SchedulesActive);
                await _settings.SetBoolSettingAsync(SettingKeys.ValueRatesActive, ValueRatesActive);
                await _settings.SetBoolSettingAsync(SettingKeys.CashInActive, CashInActive);

                // Fail closed between scalar writes: the child is off before either
                // disabling the parent or committing an enabled parent/child pair.
                await _settings.SetBoolSettingAsync(SettingKeys.DeadAirAlertNoiseEnabled, false);
                await _settings.SetBoolSettingAsync(
                    SettingKeys.DeadAirNotificationEnabled,
                    DeadAirNotificationEnabled);

                if (normalizedDeadAirAlertNoise)
                {
                    await _settings.SetBoolSettingAsync(
                        SettingKeys.DeadAirAlertNoiseEnabled,
                        true);
                }

                SettingsProvider.UpdateDashboardActive(DashboardActive);
                SettingsProvider.UpdateDashboardScreenOrder(ParseInt(DashboardScreenOrderText, 1));

                SettingsProvider.UpdateMainQuestActive(MainQuestActive);
                SettingsProvider.UpdateMainQuestScreenOrder(ParseInt(MainQuestScreenOrderText, 2));

                SettingsProvider.UpdateMissionActive(MissionActive);
                SettingsProvider.UpdateMissionScreenOrder(ParseInt(MissionScreenOrderText, 3));

                SettingsProvider.UpdateBudgetsActive(BudgetsActive);
                SettingsProvider.UpdateBudgetsScreenOrder(ParseInt(BudgetsScreenOrderText, 4));

                SettingsProvider.UpdateAchievementsActive(AchievementsActive);
                SettingsProvider.UpdateAchievementsScreenOrder(ParseInt(AchievementsScreenOrderText, 5));

                SettingsProvider.UpdateArcsActive(ArcsActive);
                SettingsProvider.UpdateArcsScreenOrder(ParseInt(ArcsScreenOrderText, 6));

                SettingsProvider.UpdateGoalsActive(GoalsActive);
                SettingsProvider.UpdateGoalsScreenOrder(ParseInt(GoalsScreenOrderText, 7));

                SettingsProvider.UpdateLocksEnabled(LocksActive);
                SettingsProvider.UpdateSchedulesEnabled(SchedulesActive);
                SettingsProvider.UpdateValueRatesEnabled(ValueRatesActive);
                SettingsProvider.UpdateCashInEnabled(CashInActive);
                SettingsProvider.UpdateDeadAirAlertNoiseEnabled(false);
                SettingsProvider.UpdateDeadAirNotificationEnabled(DeadAirNotificationEnabled);

                if (normalizedDeadAirAlertNoise)
                    SettingsProvider.UpdateDeadAirAlertNoiseEnabled(true);

                if (_reconcileNotificationAsync != null)
                    await _reconcileNotificationAsync();

                if (_onSaved != null)
                    await _onSaved();
            }
            finally
            {
                IsSaving = false;
            }
        }

        public async Task RefreshNotificationAvailabilityAsync(
            CancellationToken cancellationToken = default)
        {
            ActiveCardNotificationAvailability availability;

            try
            {
                availability = await _notificationAvailability.GetAvailabilityAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Could not check active-card notification availability: {ex}");
                availability = ActiveCardNotificationAvailability.Unknown;
            }

            if (_activeCardNotificationAvailability == availability)
                return;

            _activeCardNotificationAvailability = availability;
            NotifyDeadAirAlertStateChanged();
        }

        private async Task OpenNotificationSettingsAsync()
        {
            if (!CanOpenNotificationSettings)
                return;

            await _notificationAvailability.OpenNotificationSettingsAsync();
        }

        private void SetDeadAirAlertNoiseEnabled(
            bool value,
            bool allowUnavailableEnable)
        {
            if (value && !DeadAirNotificationEnabled)
                return;

            if (value &&
                !_deadAirAlertNoiseEnabled &&
                !allowUnavailableEnable &&
                !IsActiveCardNotificationAvailable)
            {
                return;
            }

            if (!SetProperty(ref _deadAirAlertNoiseEnabled, value, nameof(DeadAirAlertNoiseEnabled)))
                return;

            NotifyDeadAirAlertStateChanged();
        }

        private void NotifyDeadAirAlertStateChanged()
        {
            RaisePropertyChanged(nameof(IsActiveCardNotificationAvailable));
            RaisePropertyChanged(nameof(CanChangeDeadAirAlertNoise));
            RaisePropertyChanged(nameof(IsDeadAirAlertAvailabilityWarningVisible));
            RaisePropertyChanged(nameof(CanOpenNotificationSettings));
            RaisePropertyChanged(nameof(DeadAirAlertAvailabilityMessage));
            _openNotificationSettingsCommand.NotifyCanExecuteChanged();
        }

        private void NotifySaveStateChanged()
        {
            RaisePropertyChanged(nameof(CanSave));
            _saveCommand.NotifyCanExecuteChanged();
        }

        private static bool GetBool(List<AcquiredSetting> settings, string key, bool defaultValue)
        {
            return settings.FirstOrDefault(x => x.SettingKey == key)?.BoolValue ?? defaultValue;
        }

        private static int GetInt(List<AcquiredSetting> settings, string key, int defaultValue)
        {
            return settings.FirstOrDefault(x => x.SettingKey == key)?.IntValue ?? defaultValue;
        }

        private static bool IsValidInt(string value)
        {
            return int.TryParse(value, out _);
        }

        private static int ParseInt(string value, int defaultValue)
        {
            return int.TryParse(value, out var parsed) ? parsed : defaultValue;
        }
    }
}
