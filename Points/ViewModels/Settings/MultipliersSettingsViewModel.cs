using Points.Global;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace Points.ViewModels.Settings
{
    public class MultipliersSettingsViewModel : Models.ObservableObject, INotifyPropertyChanged
    {
        private readonly ISettingsService _settings;
        private readonly IHardModePenaltyService _hardModePenalties;
        private readonly IUserMultiplierService _userMultipliers;
        private readonly IClock _clock;
        private readonly Func<Task>? _onSaved;
        private readonly Command _saveCommand;
        private readonly Command _addMultiplierCommand;
        private bool _isLoadingMultipliers;

        public MultipliersSettingsViewModel(
            ISettingsService settings,
            IHardModePenaltyService hardModePenalties,
            IUserMultiplierService userMultipliers,
            IClock clock,
            Func<Task>? onSaved = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _hardModePenalties = hardModePenalties ?? throw new ArgumentNullException(nameof(hardModePenalties));
            _userMultipliers = userMultipliers ?? throw new ArgumentNullException(nameof(userMultipliers));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _onSaved = onSaved;

            _saveCommand = new Command(async () => await SaveAsync(), () => CanSave);
            SaveCommand = _saveCommand;

            _addMultiplierCommand = new Command(async () => await AddMultiplierAsync(), () => CanAddMultiplier);
            AddMultiplierCommand = _addMultiplierCommand;

            _ = InitializeAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand AddMultiplierCommand { get; }

        public ObservableCollection<MultiplierEditorViewModel> Multipliers { get; } = new();

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
                    RefreshCommandStates();
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
                    RefreshCommandStates();
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

        public bool CanSave => (!HardModeEnabled || IsHardModePenaltyValid)
            && Multipliers.All(x => x.IsValid);

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

            await LoadMultipliersAsync();
        }

        private string _newMultiplierName = "";
        public string NewMultiplierName
        {
            get => _newMultiplierName;
            set
            {
                if (SetProperty(ref _newMultiplierName, value ?? ""))
                    RefreshNewMultiplierState();
            }
        }

        private string _newMultiplierCode = "";
        public string NewMultiplierCode
        {
            get => _newMultiplierCode;
            set
            {
                if (SetProperty(ref _newMultiplierCode, value ?? ""))
                    RefreshNewMultiplierState();
            }
        }

        private string _newMultiplierDescription = "";
        public string NewMultiplierDescription
        {
            get => _newMultiplierDescription;
            set => SetProperty(ref _newMultiplierDescription, value ?? "");
        }

        private string _newMultiplierMultiplyByText = "1.0";
        public string NewMultiplierMultiplyByText
        {
            get => _newMultiplierMultiplyByText;
            set
            {
                if (SetProperty(ref _newMultiplierMultiplyByText, value ?? ""))
                    RefreshNewMultiplierState();
            }
        }

        private string _customMultiplierErrorText = "";
        public string CustomMultiplierErrorText
        {
            get => _customMultiplierErrorText;
            private set
            {
                if (SetProperty(ref _customMultiplierErrorText, value ?? ""))
                    RaisePropertyChanged(nameof(HasCustomMultiplierError));
            }
        }

        public bool HasCustomMultiplierError => !string.IsNullOrWhiteSpace(CustomMultiplierErrorText);

        public bool HasMultipliers => Multipliers.Count > 0;

        public bool CanAddMultiplier => TryBuildNewMultiplier(out _, out _);

        internal void OnMultiplierChanged(MultiplierEditorViewModel multiplier)
        {
            RaisePropertyChanged(nameof(CanSave));
            RefreshCommandStates();
        }

        internal void OnMultiplierActiveChanged(MultiplierEditorViewModel multiplier, bool isActive)
        {
            if (_isLoadingMultipliers || multiplier.IsSynchronizingActiveState || multiplier.Id <= 0)
                return;

            _ = SetMultiplierActiveAsync(multiplier, isActive);
        }

        private async Task LoadMultipliersAsync()
        {
            _isLoadingMultipliers = true;

            try
            {
                var multipliers = await _userMultipliers.GetMultipliersAsync();

                Multipliers.Clear();
                foreach (var multiplier in multipliers)
                    Multipliers.Add(new MultiplierEditorViewModel(multiplier, this));

                await _userMultipliers.GetActiveMultiplierAsync();

                RaisePropertyChanged(nameof(HasMultipliers));
                RaisePropertyChanged(nameof(CanSave));
                RefreshCommandStates();
            }
            finally
            {
                _isLoadingMultipliers = false;
            }
        }

        private async Task AddMultiplierAsync()
        {
            CustomMultiplierErrorText = "";

            if (!TryBuildNewMultiplier(out var model, out var error) || model == null)
            {
                CustomMultiplierErrorText = error;
                return;
            }

            try
            {
                await _userMultipliers.SaveMultiplierAsync(model, _clock.UtcNow);

                NewMultiplierName = "";
                NewMultiplierCode = "";
                NewMultiplierDescription = "";
                NewMultiplierMultiplyByText = "1.0";

                await LoadMultipliersAsync();
            }
            catch (Exception ex)
            {
                CustomMultiplierErrorText = ex.Message;
            }
        }

        internal async Task SaveMultiplierAsync(MultiplierEditorViewModel multiplier)
        {
            CustomMultiplierErrorText = "";

            if (!multiplier.TryBuildModel(out var model, out var error) || model == null)
            {
                CustomMultiplierErrorText = error;
                return;
            }

            try
            {
                await _userMultipliers.SaveMultiplierAsync(model, _clock.UtcNow);
                await LoadMultipliersAsync();
            }
            catch (Exception ex)
            {
                CustomMultiplierErrorText = ex.Message;
            }
        }

        internal async Task DeleteMultiplierAsync(MultiplierEditorViewModel multiplier)
        {
            CustomMultiplierErrorText = "";

            try
            {
                await _userMultipliers.DeleteMultiplierAsync(multiplier.Id, _clock.UtcNow);
                await LoadMultipliersAsync();
            }
            catch (Exception ex)
            {
                CustomMultiplierErrorText = ex.Message;
            }
        }

        private async Task SetMultiplierActiveAsync(MultiplierEditorViewModel multiplier, bool isActive)
        {
            CustomMultiplierErrorText = "";

            try
            {
                await _userMultipliers.SetActiveMultiplierAsync(isActive ? multiplier.Id : null, _clock.UtcNow);
                await LoadMultipliersAsync();
            }
            catch (Exception ex)
            {
                CustomMultiplierErrorText = ex.Message;
                multiplier.SetActiveSilently(!isActive);
            }
        }

        private async Task SaveAsync()
        {
            if (!CanSave)
                return;

            CustomMultiplierErrorText = "";

            try
            {
                foreach (var multiplier in Multipliers)
                {
                    if (!multiplier.TryBuildModel(out var model, out var error) || model == null)
                    {
                        CustomMultiplierErrorText = error;
                        return;
                    }

                    await _userMultipliers.SaveMultiplierAsync(model, _clock.UtcNow);
                }

                await _settings.SetBoolSettingAsync(SettingKeys.HardModeEnabled, HardModeEnabled);
                await _settings.SetDoubleSettingAsync(SettingKeys.HardModeDamagePerMinuteValue, HardModeIdlePenaltyPerMinute);

                SettingsProvider.UpdateHardModeEnabled(HardModeEnabled);
                SettingsProvider.UpdateHardModeDamagePerMinuteValue(HardModeIdlePenaltyPerMinute);

                await _userMultipliers.GetActiveMultiplierAsync();

                await _hardModePenalties.ReconcileAsync(_clock.UtcNow);

                if (_onSaved != null) await _onSaved();
            }
            catch (Exception ex)
            {
                CustomMultiplierErrorText = ex.Message;
            }
        }

        private bool TryBuildNewMultiplier(out UserMultiplierModel? model, out string error)
        {
            model = null;
            error = "";

            if (!TryParseMultiplier(
                NewMultiplierName,
                NewMultiplierCode,
                NewMultiplierDescription,
                NewMultiplierMultiplyByText,
                out var parsed,
                out error))
            {
                return false;
            }

            model = parsed;
            return true;
        }

        internal static bool TryParseMultiplier(
            string name,
            string code,
            string description,
            string multiplyByText,
            out UserMultiplierModel? model,
            out string error)
        {
            model = null;
            error = "";

            name = (name ?? "").Trim();
            code = (code ?? "").Trim().ToUpperInvariant();
            description = (description ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Multiplier name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                error = "Multiplier code is required.";
                return false;
            }

            if (code.Length > 3)
            {
                error = "Multiplier code must be 3 characters or fewer.";
                return false;
            }

            if (!double.TryParse(multiplyByText, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var multiplyBy)
                && !double.TryParse(multiplyByText, out multiplyBy))
            {
                error = "Multiply By must be a valid number.";
                return false;
            }

            if (double.IsNaN(multiplyBy) || double.IsInfinity(multiplyBy) || multiplyBy <= 0)
            {
                error = "Multiply By must be greater than 0.";
                return false;
            }

            model = new UserMultiplierModel
            {
                Name = name,
                Code = code,
                Description = description,
                MultiplyBy = multiplyBy
            };

            return true;
        }

        private void RefreshNewMultiplierState()
        {
            RaisePropertyChanged(nameof(CanAddMultiplier));
            _addMultiplierCommand.ChangeCanExecute();
        }

        private void RefreshCommandStates()
        {
            _saveCommand.ChangeCanExecute();
            _addMultiplierCommand.ChangeCanExecute();
        }
    }

    public sealed class MultiplierEditorViewModel : Models.ObservableObject
    {
        private readonly MultipliersSettingsViewModel _owner;
        private bool _isSynchronizingActiveState;

        public MultiplierEditorViewModel(
            UserMultiplierModel model,
            MultipliersSettingsViewModel owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            Id = model.Id;
            _name = model.Name;
            _code = model.Code;
            _description = model.Description;
            _multiplyByText = model.MultiplyBy.ToString(CultureInfo.InvariantCulture);
            _isActive = model.IsActive;

            SaveCommand = new Command(async () => await _owner.SaveMultiplierAsync(this), () => IsValid);
            DeleteCommand = new Command(async () => await _owner.DeleteMultiplierAsync(this));
        }

        public int Id { get; }

        public ICommand SaveCommand { get; }

        public ICommand DeleteCommand { get; }

        public bool IsSynchronizingActiveState => _isSynchronizingActiveState;

        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value ?? ""))
                    OnEditablePropertyChanged();
            }
        }

        private string _code = "";
        public string Code
        {
            get => _code;
            set
            {
                if (SetProperty(ref _code, value ?? ""))
                    OnEditablePropertyChanged();
            }
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value ?? ""))
                    OnEditablePropertyChanged();
            }
        }

        private string _multiplyByText = "1.0";
        public string MultiplyByText
        {
            get => _multiplyByText;
            set
            {
                if (SetProperty(ref _multiplyByText, value ?? ""))
                    OnEditablePropertyChanged();
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                {
                    RaisePropertyChanged(nameof(ActiveStatusText));
                    RaisePropertyChanged(nameof(ActiveStatusColor));

                    if (!_isSynchronizingActiveState)
                        _owner.OnMultiplierActiveChanged(this, value);
                }
            }
        }

        public bool CanToggle => Id > 0;

        public string ActiveStatusText => IsActive ? "Active" : "Inactive";

        public string ActiveStatusColor => IsActive ? "#F59E0B" : "#777777";

        public bool IsValid => TryBuildModel(out _, out _);

        public string ValidationMessage
        {
            get
            {
                TryBuildModel(out _, out var error);
                return error;
            }
        }

        public bool HasValidationMessage => !IsValid;

        public void SetActiveSilently(bool value)
        {
            _isSynchronizingActiveState = true;
            try
            {
                IsActive = value;
            }
            finally
            {
                _isSynchronizingActiveState = false;
            }
        }

        public bool TryBuildModel(out UserMultiplierModel? model, out string error)
        {
            if (!MultipliersSettingsViewModel.TryParseMultiplier(
                Name,
                Code,
                Description,
                MultiplyByText,
                out model,
                out error))
            {
                return false;
            }

            model!.Id = Id;
            model.IsActive = IsActive;
            return true;
        }

        private void OnEditablePropertyChanged()
        {
            RaisePropertyChanged(nameof(IsValid));
            RaisePropertyChanged(nameof(ValidationMessage));
            RaisePropertyChanged(nameof(HasValidationMessage));

            if (SaveCommand is Command saveCommand)
                saveCommand.ChangeCanExecute();

            _owner.OnMultiplierChanged(this);
        }
    }
}
