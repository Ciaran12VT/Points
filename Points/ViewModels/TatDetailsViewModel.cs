using Points.Global;
using Points.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class TatDetailsViewModel : ObservableObject
    {
        private TatCardModel _model;
        private Action<TatCardModel> _onSaved;
        private Action<TatCardModel> _onDelete;

        public Command CancelCommand { get; }

        public TatDetailsViewModel(TatCardModel model, Action<TatCardModel> onSaved, Action<TatCardModel> onDelete)
        {
            _onSaved = onSaved;
            _onDelete = onDelete;
            ToggleSignCommand = new Command(ToggleSign);
            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await OnCancelAsync());
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

            RaiseComputed();
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
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

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
        public string SignToggleText => _isNegative ? "Negative" : "Positive";

        public Command ToggleSignCommand { get; }
        private void ToggleSign()
        {
            _isNegative = !_isNegative;
            RaisePropertyChanged(nameof(SignToggleText));
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

        private void RaiseComputed()
        {
            RaisePropertyChanged(nameof(CurrentAccruedValueText));
            RaisePropertyChanged(nameof(ActiveTimeText));
        }
    }
}
