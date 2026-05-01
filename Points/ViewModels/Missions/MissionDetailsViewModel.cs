using Points.Global;
using Points.ViewModels.Shared;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels.Missions
{
    public class MissionDetailsViewModel : ObservableObject
    {
        private readonly MissionCardModel _model;
        private readonly Action<MissionCardModel> _onSaved;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly ActiveCardDetailsInteractionCoordinator _detailsInteractions;
        private readonly IActivityService _activity;
        private readonly IUdmdService _udmd;
        private readonly IClock _clock;

        private readonly Action<MissionCardModel> _onDelete;
        private readonly Action<MissionCardModel> _onFail;
        private bool _suspendDueAdjustment;

        public List<string> AvailableTagList { get; }
        public Command SaveCommand { get; }
        public Command CancelCommand { get; }
        public Command EditTagsCommand { get; }
        public Command ClearTagsCommand { get; }
        public Command EditEstimatedTimeCommand { get; }
        public Command EditActiveTimeCommand { get; }
        public Command SetActiveTimeTargetCommand { get; }
        public Command AddResourceImagesCommand { get; }
        public Command AddResourceFilesCommand { get; }

        public bool IsReadOnly => _model.IsComplete;     // complete => read-only
        public bool CanEdit => !_model.IsComplete;       // convenience
        public string ActiveTimeText => _model.GetActiveTime(GlobalVariables.RangeStart, GlobalVariables.RangeEnd).ToString(@"hh\:mm\:ss");

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        public MissionDetailsViewModel(
            MissionCardModel model,
            Action<MissionCardModel> onSaved,
            Action<MissionCardModel> onDelete,
            Action<MissionCardModel> onFail,
            List<string> availableTagsList,
            IActivityService activity,
            IUdmdService udmd,
            IClock clock,
            ITimeZoneService? timeZoneService = null,
            IAppNavigationService? navigation = null,
            IAppDialogService? dialogs = null)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete;
            _onFail = onFail;
            _timeZoneService = timeZoneService ?? new TimeZoneService();
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _detailsInteractions = new ActiveCardDetailsInteractionCoordinator(_navigation, _dialogs, _timeZoneService, _clock);
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
            EditTagsCommand = new Command(async () => await EditTagsAsync());
            ClearTagsCommand = new Command(ClearTags);
            EditEstimatedTimeCommand = new Command(async () => await EditEstimatedTimeAsync());
            EditActiveTimeCommand = new Command(async () => await EditActiveTimeAsync());
            SetActiveTimeTargetCommand = new Command(async () => await EditEstimatedTimeAsync());
            AddResourceImagesCommand = new Command(async () => await AddResourceImagesAsync());
            AddResourceFilesCommand = new Command(async () => await AddResourceFilesAsync());

            // Read-only
            CreatedDateText = TimeDisplayFormatter.FormatInstant(
                _model.CreatedDate,
                "yyyy-MM-dd HH:mm:ss",
                _timeZoneService,
                CultureInfo.InvariantCulture);
            CompletedDateText = TimeDisplayFormatter.FormatNullableInstant(
                _model.CompletedDate,
                "yyyy-MM-dd HH:mm:ss",
                "--",
                _timeZoneService,
                CultureInfo.InvariantCulture);

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
            _suspendDueAdjustment = true;
            AvailableFromDate = _model.AvailableFromDate.Date;
            AvailableFromTime = _model.AvailableFromDate.TimeOfDay;

            DueDate = _model.DueDate.Date;
            DueTime = _model.DueDate.TimeOfDay;
            _suspendDueAdjustment = false;

            // NEW: Event date
            HasEventDate = _model.EventDate.HasValue;
            var eventDate = _model.EventDate ?? _clock.LocalNow.Date;
            EventDateValue = eventDate.Date;                // just the date
            EventTimeValue = eventDate.TimeOfDay;          // time part (00:00 if none)

            //Resources
            ViewResourcesCommand = new Command(async () => await OnViewResourcesAsync());
            ClearResourcesCommand = new Command(async () => await OnClearResourcesAsync());

            // initial count from disk
            RefreshResourceCount();

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
            set
            {
                if (SetProperty(ref _availableFromDate, value))
                    EnsureDueAfterAvailable();
            }
        }

        private TimeSpan _availableFromTime;
        public TimeSpan AvailableFromTime
        {
            get => _availableFromTime;
            set
            {
                if (SetProperty(ref _availableFromTime, value))
                    EnsureDueAfterAvailable();
            }
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

        #region Resources

        public ObservableCollection<string> ResourcesToAdd { get; } = new();

        private int _resourceCount;
        public int ResourceCount
        {
            get => _resourceCount;
            private set => SetProperty(ref _resourceCount, value);
        }

        public Command ViewResourcesCommand { get; }
        public Command ClearResourcesCommand { get; }

        private string GetResourceFolder()
        {
            // Ensures the folder exists
            return AppPaths.GetMissionResourcesPath(_model.Id);
        }

        private void RefreshResourceCount()
        {
            try
            {
                var folder = GetResourceFolder();
                ResourceCount = Directory.Exists(folder)
                    ? Directory.EnumerateFiles(folder).Count()
                    : 0;
            }
            catch
            {
                ResourceCount = 0;
            }
        }

        private List<string> GetSavedResourceFiles()
        {
            var folder = GetResourceFolder();
            if (!Directory.Exists(folder)) return new List<string>();

            return Directory.EnumerateFiles(folder)
                .Where(File.Exists)
                .ToList();
        }

        private async Task SaveResourcesToDiskAsync()
        {
            if (ResourcesToAdd.Count == 0) return;

            var targetFolder = GetResourceFolder(); // ensures exists

            // De-dupe by source path, ignore empties
            var sources = ResourcesToAdd
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            foreach (var src in sources)
            {
                try
                {
                    if (!File.Exists(src)) continue;

                    var originalName = Path.GetFileName(src);

                    // Avoid collisions: GUID prefix
                    var destName = $"{Guid.NewGuid():N}_{originalName}";
                    var destPath = Path.Combine(targetFolder, destName);

                    // Stream copy (better than ReadAllBytes for large files)
                    await using var inStream = File.OpenRead(src);
                    await using var outStream = File.Create(destPath);
                    await inStream.CopyToAsync(outStream);
                }
                catch (Exception ex)
                {
                    // Non-fatal: skip this file and continue
                    await _dialogs.DisplayAlertAsync("Resource Save Failed",
                        $"Could not save:\n{Path.GetFileName(src)}\n\n{ex.Message}",
                        "OK");
                }
            }

            // Clear pending so Save is idempotent
            ResourcesToAdd.Clear();
        }

        private async Task OnViewResourcesAsync()
        {
            var files = GetSavedResourceFiles();

            if (files.Count == 0)
            {
                await _dialogs.DisplayAlertAsync("Resources", "No resources saved.", "OK");
                return;
            }

            // DisplayActionSheet returns the selected label string
            // We list by filename, but map back to full paths.
            var names = files
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray();

            var selectedName = await _dialogs.DisplayActionSheetAsync(
                "Resources",
                "Close",
                null,
                names);

            if (string.IsNullOrWhiteSpace(selectedName) || selectedName == "Close")
                return;

            var selectedPath = files.FirstOrDefault(f => Path.GetFileName(f) == selectedName);
            if (selectedPath == null || !File.Exists(selectedPath))
            {
                await _dialogs.DisplayAlertAsync("Open Failed", "That file no longer exists.", "OK");
                RefreshResourceCount();
                return;
            }

            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(selectedPath)
                });
            }
            catch (Exception ex)
            {
                await _dialogs.DisplayAlertAsync("Open Failed", ex.Message, "OK");
            }
        }

        private async Task OnClearResourcesAsync()
        {
            var files = GetSavedResourceFiles();

            if (files.Count == 0 && ResourcesToAdd.Count == 0)
            {
                await _dialogs.DisplayAlertAsync("Resources", "No resources to clear.", "OK");
                return;
            }

            var confirm = await _dialogs.DisplayAlertAsync(
                "Clear Resources",
                "This will permanently delete all saved resources for this mission. Continue?",
                "Delete",
                "Cancel");

            if (!confirm) return;

            try
            {
                // Clear pending picks too
                ResourcesToAdd.Clear();

                foreach (var f in files)
                {
                    try { File.Delete(f); }
                    catch { /* keep going */ }
                }

                RefreshResourceCount();
            }
            catch (Exception ex)
            {
                await _dialogs.DisplayAlertAsync("Clear Failed", ex.Message, "OK");
            }
        }



        #endregion

        private async Task EditTagsAsync()
        {
            if (!CanEdit)
                return;

            var tags = await _detailsInteractions.PickTagsAsync(AvailableTagList, Tags);
            if (tags != null)
                Tags = tags;
        }

        private void ClearTags()
        {
            if (CanEdit)
                Tags = "";
        }

        private async Task EditEstimatedTimeAsync()
        {
            if (!CanEdit)
                return;

            var result = await _detailsInteractions.PickDurationAsync(
                EstimatedTimeTs == TimeSpan.Zero ? null : EstimatedTimeTs);

            if (result is null)
                return;

            EstimatedTimeTs = result.Value;
            EstimatedTimeText = FormatDuration(result.Value);
        }

        private async Task EditActiveTimeAsync()
        {
            await _detailsInteractions.EditActiveTimeAsync(_model, _activity, _udmd);
            RaisePropertyChanged(nameof(ActiveTimeText));
        }

        private async Task AddResourceImagesAsync()
        {
            if (!CanEdit)
                return;

            var paths = await _detailsInteractions.PickFilePathsAsync(
                "Pick images (resources)",
                FilePickerFileType.Images);

            AddPendingResources(paths);
        }

        private async Task AddResourceFilesAsync()
        {
            if (!CanEdit)
                return;

            var paths = await _detailsInteractions.PickFilePathsAsync("Pick files (resources)");
            AddPendingResources(paths);
        }

        private void AddPendingResources(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                    ResourcesToAdd.Add(path);
            }
        }

        private void EnsureDueAfterAvailable()
        {
            if (_suspendDueAdjustment)
                return;

            var available = AvailableFromDate.Date + AvailableFromTime;
            var due = DueDate.Date + DueTime;

            if (available <= due)
                return;

            var newDue = available.AddHours(224);
            DueDate = newDue.Date;
            DueTime = newDue.TimeOfDay;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            var totalHours = (int)duration.TotalHours;
            return $"{totalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        private async Task SaveAsync()
        {
            // Compose DateTime values
            var available = AvailableFromDate.Date + AvailableFromTime;
            var due = DueDate.Date + DueTime;

            // Optional guardrails: ensure due >= available
            if (due < available)
            {
                await _dialogs.DisplayAlertAsync("Invalid Dates", "Due By must be after Available From.", "OK");
                return;
            }

            // Apply edits back to model
            if (string.IsNullOrEmpty(Title))
            {
                await _dialogs.DisplayAlertAsync("Missing Title", "Please fill in the Title.", "OK");
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
                await _dialogs.DisplayAlertAsync("Missing Est Time", "Please estimate the time required.", "OK");
                return;
            }

            _model.EstCompletionTime = EstimatedTimeTs;

            if (!double.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                await _dialogs.DisplayAlertAsync("Invalid Value", "Please enter a valid numeric value.", "OK");
                return;
            }

            _model.Value = value;

            if (!double.TryParse(ValuePerMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out var valuePerMin))
            {
                await _dialogs.DisplayAlertAsync("Invalid Value Per Minute", "Please enter a valid numeric value.", "OK");
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


            await SaveResourcesToDiskAsync();
            RefreshResourceCount();


            // CreatedDate stays as originally set (auto)
            // Status stays non-editable here
            // CompletedDate stays controlled by completion button

            _onSaved(_model);

            await _navigation.PopAsync();
        }

        private async Task OnCancelAsync()
        {
            var choice = await _dialogs.DisplayActionSheetAsync(
                _model.Title,
                "Cancel",
                null,
                "Delete",
                "Failed"
            );

            if (choice == "Delete")
            {
                _onDelete?.Invoke(_model);
                await _navigation.PopAsync();
            }
            else if (choice == "Failed")
            {
                _onFail?.Invoke(_model);
                await _navigation.PopAsync();
            }
        }
    }
}
