using Points.Global;
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

        public List<string> AvailableTagList { get; }
        public Command SaveCommand { get; }
        public Command CancelCommand { get; }

        public bool IsReadOnly => _model.IsComplete;     // complete => read-only
        public bool CanEdit => !_model.IsComplete;       // convenience
        public string ActiveTimeText => _model.GetActiveTime(GlobalVariables.RangeStart, GlobalVariables.RangeEnd).ToString(@"hh\:mm\:ss");

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        public MissionDetailsViewModel(MissionCardModel model, Action<MissionCardModel> onSaved, Action<MissionCardModel> onDelete, Action<MissionCardModel> onFail, List<string> availableTagsList)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete;
            _onFail = onFail;
            AvailableTagList = availableTagsList;

            // Tick every second
            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, __) =>
            {
                RaisePropertyChanged(nameof(ActiveTimeText));
            };
            _timer.Start();

            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await OnCancelAsync());

            // Read-only
            CreatedDateText = _model.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            CompletedDateText = _model.CompletedDate.HasValue ? _model.CompletedDate.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "--";

            EstimatedTimeText = _model.EstCompletionTimeText;
            EstimatedTimeTs = _model.EstCompletionTime.HasValue ? _model.EstCompletionTime.Value : TimeSpan.Zero;

            // Editable copies
            Title = _model.Title;
            Tags = _model.Tags;
            Description = _model.Description;
            ValueText = _model.Value.ToString("0.##", CultureInfo.InvariantCulture);
            ValuePerMinText = _model.ValuePerMinute.ToString("0.##", CultureInfo.InvariantCulture);

            SubTypeOptions = new ObservableCollection<MissionSubType>(
                Enum.GetValues<MissionSubType>());

            SelectedSubType = _model.SubType;

            // Break datetimes into date + time pickers
            AvailableFromDate = _model.AvailableFromDate.Date;
            AvailableFromTime = _model.AvailableFromDate.TimeOfDay;

            DueDate = _model.DueDate.Date;
            DueTime = _model.DueDate.TimeOfDay;

            // NEW: Event date
            HasEventDate = _model.EventDate.HasValue;
            var eventDate = _model.EventDate ?? DateTime.Today;
            EventDateValue = eventDate.Date;                // just the date
            EventTimeValue = eventDate.TimeOfDay;          // time part (00:00 if none)
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

        private string _valuePerMinText = "0";
        public string ValuePerMinText { get => _valuePerMinText; set => SetProperty(ref _valuePerMinText, value); }


        private string _estimatedTimeText = "00:00:00";
        public string EstimatedTimeText { get => _estimatedTimeText; set => SetProperty(ref _estimatedTimeText, value); }

        private TimeSpan _estimatedTimeTs = TimeSpan.Zero;
        public TimeSpan EstimatedTimeTs { get => _estimatedTimeTs; set => SetProperty(ref _estimatedTimeTs, value); }

        public ObservableCollection<MissionSubType> SubTypeOptions { get; }

        private MissionSubType _selectedSubType;
        public MissionSubType SelectedSubType
        {
            get => _selectedSubType;
            set => SetProperty(ref _selectedSubType, value);
        }

        // Read-only
        public string Status => _model.Status;

        public bool IsComplete => _model.IsComplete;

        public string CreatedDateText { get; }

        public string CompletedDateText { get; }

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

        // NEW: Event Date + checkbox
        private DateTime _eventDateValue;
        public DateTime EventDateValue
        {
            get => _eventDateValue;
            set => SetProperty(ref _eventDateValue, value);
        }

        private TimeSpan _eventTimeValue;
        public TimeSpan EventTimeValue
        {
            get => _eventTimeValue;
            set => SetProperty(ref _eventTimeValue, value);
        }

        private bool _hasEventDate;
        public bool HasEventDate
        {
            get => _hasEventDate;
            set => SetProperty(ref _hasEventDate, value);
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
            if (string.IsNullOrEmpty(Title))
            {
                await Shell.Current.DisplayAlert("Missing Title", "Please fill in the Title.", "OK");
                return;
            }

            _model.Title = Title;
            _model.Tags = Tags;
            _model.Description = Description;

            _model.SubType = SelectedSubType;
            _model.AvailableFromDate = available;
            _model.DueDate = due;

            if (EstimatedTimeTs == TimeSpan.Zero)
            {
                await Shell.Current.DisplayAlert("Missing Est Time", "Please estimate the time required.", "OK");
                return;
            }

            _model.EstCompletionTime = EstimatedTimeTs;

            if (!double.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                await Shell.Current.DisplayAlert("Invalid Value", "Please enter a valid numeric value.", "OK");
                return;
            }

            _model.Value = value;

            if (!double.TryParse(ValuePerMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out var valuePerMin))
            {
                await Shell.Current.DisplayAlert("Invalid Value Per Minute", "Please enter a valid numeric value.", "OK");
                return;
            }

            _model.ValuePerMinute = valuePerMin;

            // NEW: apply Event Date
            if (HasEventDate)
            {
                // Combine date + time into a single DateTime
                _model.EventDate = EventDateValue.Date + EventTimeValue;
            }
            else
            {
                _model.EventDate = null;
            }


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
