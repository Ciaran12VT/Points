using Points.Global;
using Points.ViewModels.Shared;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Views.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.ViewModels.Tat
{
    public class TatDetailsViewModel : ObservableObject
    {
        private TatCardModel _model = null!;
        private Action<TatCardModel> _onSaved;
        private Action<TatCardModel> _onDelete;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly ActiveCardDetailsInteractionCoordinator _detailsInteractions;
        private readonly ILockService _locks;
        private readonly IActivityService _activity;
        private readonly IUdmdService _udmd;
        private readonly List<DependencyTaskOption> _dependencyOptions;

        private TimeSpan? _targetActiveTime;
        public TimeSpan? TargetActiveTime
        {
            get => _targetActiveTime;
            set
            {
                if (SetProperty(ref _targetActiveTime, value))
                {
                    RaisePropertyChanged(nameof(HasTargetActiveTime));
                    RaisePropertyChanged(nameof(ActiveTimeTargetLabelColor));
                }
            }
        }
        public bool HasTargetActiveTime => TargetActiveTime != null;
        public string ActiveTimeTargetLabelColor => HasTargetActiveTime ? "Yellow" : "White";

        public ObservableCollection<ValueRateModel> ValueRates { get; } = new();

        public Command CancelCommand { get; }

        public Command AddValueRateCommand { get; }
        public Command EditTagsCommand { get; }
        public Command ClearTagsCommand { get; }
        public Command EditActiveTimeCommand { get; }
        public Command EditSchedulesCommand { get; }
        public Command EditLocksCommand { get; }
        public Command EditUdmdCommand { get; }
        public Command SetActiveTimeTargetCommand { get; }

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        public bool IsLocksEnabled
        {
            get => _model.IsLocksEnabled;
            set
            {
                if (_model.IsLocksEnabled != value)
                {
                    _model.IsLocksEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }
        public bool IsValueRatesEnabled
        {
            get => _model.IsValueRatesEnabled;
            set
            {
                if (_model.IsValueRatesEnabled != value)
                {
                    _model.IsValueRatesEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsSchedulesEnabled
        {
            get => _model.IsSchedulesEnabled;
            set
            {
                if (_model.IsSchedulesEnabled != value)
                {
                    _model.IsSchedulesEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }

        public TatDetailsViewModel(
            TatCardModel model,
            Action<TatCardModel> onSaved,
            Action<TatCardModel> onDelete,
            List<string> availableTagsList,
            ILockService locks,
            IActivityService activity,
            IUdmdService udmd,
            List<DependencyTaskOption> dependencyOptions,
            IClock clock,
            ITimeZoneService timeZoneService,
            IAppNavigationService navigation,
            IAppDialogService dialogs)
        {
            _onSaved = onSaved;
            _onDelete = onDelete;
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _detailsInteractions = new ActiveCardDetailsInteractionCoordinator(
                _navigation,
                _dialogs,
                timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService)),
                clock ?? throw new ArgumentNullException(nameof(clock)));
            _locks = locks ?? throw new ArgumentNullException(nameof(locks));
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
            _dependencyOptions = dependencyOptions ?? throw new ArgumentNullException(nameof(dependencyOptions));
            ToggleSignCommand = new Command(ToggleSign);
            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await OnCancelAsync());
            AddValueRateCommand = new Command(AddValueRate);
            EditTagsCommand = new Command(async () => await EditTagsAsync());
            ClearTagsCommand = new Command(ClearTags);
            EditActiveTimeCommand = new Command(async () => await EditActiveTimeAsync());
            EditSchedulesCommand = new Command(async () => await EditSchedulesAsync());
            EditLocksCommand = new Command(async () => await EditLocksAsync());
            EditUdmdCommand = new Command(async () => await EditUdmdAsync());
            SetActiveTimeTargetCommand = new Command(async () => await SetActiveTimeTargetAsync());
            AvailableTagList = availableTagsList;

            // Tick every second
            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, __) =>
            {
                RaisePropertyChanged(nameof(ActiveTimeText));
            };
            _timer.Start();

            BuildModel(model);
        }

        private void BuildModel(TatCardModel model)
        {
            _model = model;

            // seed editable fields from model
            Title = _model.Title;
            Tags = _model.Tags;
            Description = _model.Description;
            ValuePerMinuteText = Math.Abs(_model.ValuePerMinute).ToString("0.##", CultureInfo.InvariantCulture);
            _isNegative = _model.ValuePerMinute < 0;
            TargetActiveTime = _model.TargetActiveTime;

            // Copy steps into a local collection (edit freely, commit on save)
            foreach (var r in _model.ValueRates)
            {
                var rate = new ValueRateModel(DeleteValueRate) {Id = r.Id, RateName = r.RateName, ValuePerMinute = r.ValuePerMinute };
                HookRate(rate);
                ValueRates.Add(rate);
            }

            RaiseComputed();
        }

        private void DeleteValueRate(ValueRateModel rate)
        {
            ValueRates.Remove(rate);
        }


        private DateTime _rangeStart = GlobalVariables.RangeStart;
        public DateTime RangeStart
        {
            get => _rangeStart;
            set
            {
                if (_rangeStart == value) return;
                _rangeStart = value;
                RaisePropertyChanged();
            }
        }

        private DateTime _rangeEnd = GlobalVariables.RangeEnd;
        public DateTime RangeEnd
        {
            get => _rangeEnd;
            set
            {
                if (_rangeEnd == value) return;
                _rangeEnd = value;
                RaisePropertyChanged();
            }
        }


        // Editable fields (local copy)
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _tags = "";
        public string Tags { get => _tags; set
            {
                SetProperty(ref _tags, value);
            }
        }

        public List<string> AvailableTagList = new List<string>();

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _valuePerMinuteText = "";
        public string ValuePerMinuteText
        {
            get => _valuePerMinuteText;
            set
            {
                if (SetProperty(ref _valuePerMinuteText, value))
                    RaiseComputed();
            }
        }

        // Read-only display fields
        public string Status => _model.Status;

        public string ActiveTimeText
            => _model.GetActiveTime(GlobalVariables.RangeStart, GlobalVariables.RangeEnd).ToString(@"hh\:mm\:ss");

        public string CurrentAccruedValueText
            => _model.GetValue(GlobalVariables.RangeStart, GlobalVariables.RangeEnd).ToString("F2");

        public string ScheduleSummaryText => FormatCount(_model.Schedules.Count, "schedule");

        public string LocksSummaryText => FormatCount(_model.Locks.Count, "lock");

        private string _errorText = "";
        public string ErrorText
        {
            get => _errorText;
            private set
            {
                if (SetProperty(ref _errorText, value))
                    RaisePropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

        // Sign toggle
        private bool _isNegative;
        public string SignToggleText => _isNegative ? "-" : "+";
        public string SignToggleColor => _isNegative ? "Red" : "Green";

        public Command ToggleSignCommand { get; }
        private void ToggleSign()
        {
            _isNegative = !_isNegative;
            RaisePropertyChanged(nameof(SignToggleText));
            RaisePropertyChanged(nameof(SignToggleColor));
            RaiseComputed();
        }

        public Command SaveCommand { get; }
        private async Task SaveAsync()
        {
            // Parse numeric
            if (!double.TryParse(ValuePerMinuteText, NumberStyles.Float, CultureInfo.InvariantCulture, out var vpmAbs))
                vpmAbs = 0;

            var vpm = _isNegative ? -Math.Abs(vpmAbs) : Math.Abs(vpmAbs);



            // Apply to model
            _model.Title = Title;
            _model.Tags = Tags;
            _model.Description = Description;
            _model.ValuePerMinute = vpm;
            _model.TargetActiveTime = TargetActiveTime;

            // Commit steps back to model
            _model.ValueRates.Clear();
            foreach (var v in ValueRates)
            {
                _model.ValueRates.Add(new ValueRateModel
                {
                    Id = v.Id,
                    RateName = v.RateName,
                    ValuePerMinute = _isNegative ? (v.ValuePerMinute < 0 ? v.ValuePerMinute : v.ValuePerMinute * -1) : (v.ValuePerMinute < 0 ? v.ValuePerMinute * -1 : v.ValuePerMinute )
                });
            }

            if (_onSaved != null) _onSaved(_model);

            await _navigation.PopAsync();
        }

        private async Task OnCancelAsync()
        {
            var choice = await _dialogs.DisplayActionSheetAsync(
                _model.Title,
                "Cancel",
                null,
                "Delete"
            );

            if (choice == "Delete")
            {
                _onDelete?.Invoke(_model);
                await _navigation.PopAsync();
            }
        }

        private void AddValueRate()
        {
            var rate = new ValueRateModel { RateName="", ValuePerMinute=0};
            HookRate(rate);
            ValueRates.Add(rate);

            RaiseComputed();
        }

        private async Task EditTagsAsync()
        {
            var tags = await _detailsInteractions.PickTagsAsync(AvailableTagList, Tags);
            if (tags != null)
                Tags = tags;
        }

        private void ClearTags()
        {
            Tags = "";
        }

        private async Task EditActiveTimeAsync()
        {
            await _detailsInteractions.EditActiveTimeAsync(_model, _activity, _udmd);
            RaiseComputed();
        }

        private async Task EditSchedulesAsync()
        {
            ClearError();

            await _detailsInteractions.EditSchedulesAsync(
                _model.CardID,
                _model.Schedules,
                RefreshScheduleSummary,
                ShowError,
                "Please tap OK to save the tracker first, then add schedules.");
        }

        private async Task EditLocksAsync()
        {
            ClearError();

            await _detailsInteractions.EditLocksAsync(
                _model.CardID,
                _model.Locks,
                _locks,
                _dependencyOptions,
                RefreshLocksSummary,
                ShowError);
        }

        private async Task EditUdmdAsync()
        {
            ClearError();
            await _detailsInteractions.EditUdmdAsync(_model.CardID, _udmd, ShowError);
        }

        private async Task SetActiveTimeTargetAsync()
        {
            var result = await _detailsInteractions.PickActiveTimeTargetAsync(TargetActiveTime);
            if (result.WasCancelled)
                return;

            TargetActiveTime = result.Target;
        }

        private void HookRate(ValueRateModel rate)
        {
            rate.PropertyChanged += (_, __) => RaiseComputed();
        }

        private void RaiseComputed()
        {
            RaisePropertyChanged(nameof(CurrentAccruedValueText));
            RaisePropertyChanged(nameof(ActiveTimeText));
        }

        private void RefreshScheduleSummary()
        {
            RaisePropertyChanged(nameof(ScheduleSummaryText));
        }

        private void RefreshLocksSummary()
        {
            RaisePropertyChanged(nameof(LocksSummaryText));
        }

        private void ShowError(string message)
        {
            ErrorText = message;
        }

        private void ClearError()
        {
            ErrorText = "";
        }

        private static string FormatCount(int count, string singular)
        {
            if (count == 0)
                return "None";

            return count == 1 ? $"1 {singular}" : $"{count} {singular}s";
        }
    }
}
