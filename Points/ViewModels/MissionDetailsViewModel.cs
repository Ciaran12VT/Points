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
    public class MissionDetailsViewModel : ObservableObject
    {
        private readonly MissionCardModel _model;
        private readonly Action<MissionCardModel> _onSaved;

        private readonly Action<MissionCardModel> _onDelete;
        private readonly Action<MissionCardModel> _onFail;

        public Command SaveCommand { get; }
        public Command CancelCommand { get; }

        public bool IsReadOnly => _model.IsComplete;     // complete => read-only
        public bool CanEdit => !_model.IsComplete;       // convenience


        public MissionDetailsViewModel(MissionCardModel model, Action<MissionCardModel> onSaved, Action<MissionCardModel> onDelete, Action<MissionCardModel> onFail)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete;
            _onFail = onFail;

            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await OnCancelAsync());

            // Read-only
            CreatedDateText = _model.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            // Editable copies
            Title = _model.Title;
            Tags = _model.Tags;
            Description = _model.Description;
            ValueText = _model.Value.ToString("0.##", CultureInfo.InvariantCulture);

            SubTypeOptions = new ObservableCollection<MissionSubType>(
                Enum.GetValues<MissionSubType>());

            SelectedSubType = _model.SubType;

            // Break datetimes into date + time pickers
            AvailableFromDate = _model.AvailableFromDate.Date;
            AvailableFromTime = _model.AvailableFromDate.TimeOfDay;

            DueDate = _model.DueDate.Date;
            DueTime = _model.DueDate.TimeOfDay;
        }

        // Editable
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _valueText = "0";
        public string ValueText { get => _valueText; set => SetProperty(ref _valueText, value); }


        public ObservableCollection<MissionSubType> SubTypeOptions { get; }

        private MissionSubType _selectedSubType;
        public MissionSubType SelectedSubType
        {
            get => _selectedSubType;
            set => SetProperty(ref _selectedSubType, value);
        }

        // Read-only
        public string Status => _model.Status;

        public string CreatedDateText { get; }

        // Available From (Date + Time)
        private DateTime _availableFromDate;
        public DateTime AvailableFromDate
        {
            get => _availableFromDate;
            set => SetProperty(ref _availableFromDate, value);
        }

        private TimeSpan _availableFromTime;
        public TimeSpan AvailableFromTime
        {
            get => _availableFromTime;
            set => SetProperty(ref _availableFromTime, value);
        }

        // Due By (Date + Time)
        private DateTime _dueDate;
        public DateTime DueDate
        {
            get => _dueDate;
            set => SetProperty(ref _dueDate, value);
        }

        private TimeSpan _dueTime;
        public TimeSpan DueTime
        {
            get => _dueTime;
            set => SetProperty(ref _dueTime, value);
        }

        private async Task SaveAsync()
        {
            // Compose DateTime values
            var available = AvailableFromDate.Date + AvailableFromTime;
            var due = DueDate.Date + DueTime;

            // Optional guardrails: ensure due >= available
            if (due < available)
            {
                await Shell.Current.DisplayAlert("Invalid Dates", "Due By must be after Available From.", "OK");
                return;
            }

            // Apply edits back to model
            _model.Title = Title;
            _model.Tags = Tags;
            _model.Description = Description;

            _model.SubType = SelectedSubType;
            _model.AvailableFromDate = available;
            _model.DueDate = due;

            if (!double.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                await Shell.Current.DisplayAlert("Invalid Value", "Please enter a valid numeric value.", "OK");
                return;
            }

            _model.Value = value;

            // CreatedDate stays as originally set (auto)
            // Status stays non-editable here
            // CompletedDate stays controlled by completion button

            _onSaved(_model);

            await Shell.Current.Navigation.PopAsync();
        }

        private async Task OnCancelAsync()
        {
            var choice = await Shell.Current.DisplayActionSheet(
                _model.Title,
                "Cancel",
                null,
                "Delete",
                "Failed"
            );

            if (choice == "Delete")
            {
                _onDelete?.Invoke(_model);
                await Shell.Current.Navigation.PopAsync();
            }
            else if (choice == "Failed")
            {
                _onFail?.Invoke(_model);
                await Shell.Current.Navigation.PopAsync();
            }
        }
    }
}
