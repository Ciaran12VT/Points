using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class ScDetailsViewModel : ObservableObject
    {
        private readonly ScCardModel _model;
        private readonly DateTime _rangeStart;
        private readonly DateTime _rangeEnd;

        public ObservableCollection<ScStepModel> Steps { get; } = new();

        public ScDetailsViewModel(ScCardModel model, DateTime rangeStart, DateTime rangeEnd)
        {
            _model = model;
            _rangeStart = rangeStart;
            _rangeEnd = rangeEnd;

            ToggleSignCommand = new Command(ToggleSign);
            AddStepCommand = new Command(AddStep);
            SaveCommand = new Command(async () => await SaveAsync());

            // Load editable fields
            Title = _model.Title;
            Tags = _model.Tags;
            Description = _model.Description;

            // Sign is stored in ValuePerMinute sign (magnitude not used for SC)
            _isNegative = _model.ValuePerMinute < 0;

            // Copy steps into a local collection (edit freely, commit on save)
            foreach (var s in _model.Steps.OrderBy(x => x.Order))
            {
                var step = new ScStepModel { Order = Steps.Count + 1, StepValue = 1.0 };
                HookStep(step);
                Steps.Add(step);
            }

            if (Steps.Count == 0)
                AddStep();

            RaiseComputed();
        }

        // Editable fields
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        // Read-only display fields
        public string Status => _model.Status;

        public string ActiveTimeText
            => _model.GetActiveTime(_rangeStart, _rangeEnd).ToString(@"hh\:mm\:ss");

        public string CurrentAccruedValueText
        {
            get
            {
                var sum = Steps.Sum(s => s.StepValue * s.Count);
                var signed = (_isNegative ? -1 : 1) * sum;
                return signed.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        public Color CurrentAccruedValueColor
        {
            get
            {
                if (!double.TryParse(CurrentAccruedValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return Colors.Green;
                return v < 0 ? Colors.Red : Colors.Green;
            }
        }

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

        public Command AddStepCommand { get; }
        private void AddStep()
        {
            var step = new ScStepModel { Order = Steps.Count + 1, StepValue = 1.0 };
            HookStep(step);
            Steps.Add(step);

            RaiseComputed();
        }

        public Command SaveCommand { get; }

        private async Task SaveAsync()
        {
            // Commit simple fields
            _model.Title = Title;
            _model.Tags = Tags;
            _model.Description = Description;

            // Commit sign using ValuePerMinute sign
            _model.ValuePerMinute = _isNegative ? -1 : 1;

            // Commit steps back to model
            _model.Steps.Clear();
            int order = 1;
            foreach (var s in Steps)
            {
                _model.Steps.Add(new ScStepModel
                {
                    Order = order++,
                    Title = s.Title,
                    StepValue = s.StepValue,
                    Count = s.Count
                });
            }

            await Shell.Current.Navigation.PopAsync();
        }

        private void RaiseComputed()
        {
            RaisePropertyChanged(nameof(CurrentAccruedValueText));
            RaisePropertyChanged(nameof(CurrentAccruedValueColor));
            RaisePropertyChanged(nameof(ActiveTimeText));
        }

        private void HookStep(ScStepModel step)
        {
            step.PropertyChanged += (_, __) => RaiseComputed();
        }

    }
}
