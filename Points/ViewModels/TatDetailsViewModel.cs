using Points.Global;
using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class TatDetailsViewModel : ObservableObject
    {
        private TatCardModel _model;
        private Action<TatCardModel> _onSaved;
        private Action<TatCardModel> _onDelete;

        public TimeSpan? TargetActiveTime { get; set; }
        public bool HasTargetActiveTime => TargetActiveTime != null;
        public string ActiveTimeTargetLabelColor => HasTargetActiveTime ? "Yellow" : "White";

        public ObservableCollection<ValueRateModel> ValueRates { get; } = new();

        public Command CancelCommand { get; }

        public Command AddValueRateCommand { get; }

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

        public TatDetailsViewModel(TatCardModel model, Action<TatCardModel> onSaved, Action<TatCardModel> onDelete, List<string> availableTagsList)
        {
            _onSaved = onSaved;
            _onDelete = onDelete;
            ToggleSignCommand = new Command(ToggleSign);
            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await OnCancelAsync());
            AddValueRateCommand = new Command(AddValueRate);
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

            await Shell.Current.Navigation.PopAsync();
        }

        private async Task OnCancelAsync()
        {
            var choice = await Shell.Current.DisplayActionSheet(
                _model.Title,
                "Cancel",
                null,
                "Delete"
            );

            if (choice == "Delete")
            {
                _onDelete?.Invoke(_model);
                await Shell.Current.Navigation.PopAsync();
            }
        }

        private void AddValueRate()
        {
            var rate = new ValueRateModel { RateName="", ValuePerMinute=0};
            HookRate(rate);
            ValueRates.Add(rate);

            RaiseComputed();
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
    }
}
