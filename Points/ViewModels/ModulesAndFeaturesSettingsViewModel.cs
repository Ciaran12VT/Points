using Points.Global;
using Points.Services.Sqlite.Interfaces;
using System.ComponentModel;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class ModulesAndFeaturesSettingsViewModel : Models.ObservableObject, INotifyPropertyChanged
    {
        private readonly ISettingsService _settings;
        private readonly Func<Task>? _onSaved;

        public ModulesAndFeaturesSettingsViewModel(ISettingsService settings, Func<Task>? onSaved = null)
        {
            _settings = settings;
            _onSaved = onSaved;
            SaveCommand = new Command(async () => await SaveAsync());

            _ = InitializeAsync();
        }

        public ICommand SaveCommand { get; }

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
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
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
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
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
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
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
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
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
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
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
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
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
                    RaisePropertyChanged(nameof(HasInvalidScreenOrder));
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
        }

        private async Task SaveAsync()
        {
            if (HasInvalidScreenOrder) return;

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


            if (_onSaved != null) await _onSaved();
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
