using Points.Global;
using Points.ViewModels.Shared;
using Points.Models;
using Points.Services.MissionSharing;
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
        private readonly IMissionShareService _missionShares;
        private readonly ActiveCardDetailsInteractionCoordinator _detailsInteractions;
        private readonly IActivityService _activity;
        private readonly IUdmdService _udmd;
        private readonly IClock _clock;
        private readonly HashSet<string> _resourceCaptureCachePaths = new();
        private readonly MissionEditSnapshot _originalSnapshot;

        private readonly Func<MissionCardModel, Task> _onDelete;
        private readonly Func<MissionCardModel, Task> _onFail;
        private readonly Func<MissionCardModel, Task> _onRestore;
        private bool _suspendDueAdjustment;

        public List<string> AvailableTagList { get; }
        public Command SaveCommand { get; }
        public Command CancelCommand { get; }
        public Command EditTagsCommand { get; }
        public Command ClearTagsCommand { get; }
        public Command EditEstimatedTimeCommand { get; }
        public Command EditActiveTimeCommand { get; }
        public Command SetActiveTimeTargetCommand { get; }
        public Command CaptureResourceImageCommand { get; }
        public Command AddResourceImagesCommand { get; }
        public Command AddResourceFilesCommand { get; }
        public Command ShareCommand { get; }

        public bool IsReadOnly => _model.IsComplete;     // complete => read-only
        public bool CanEdit => !_model.IsComplete;       // convenience
        public bool CanShare => _model.CardID > 0;
        public string ActiveTimeText => _model.GetActiveTime(GlobalVariables.RangeStart, GlobalVariables.RangeEnd).ToString(@"hh\:mm\:ss");

        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();

        public MissionDetailsViewModel(
            MissionCardModel model,
            Action<MissionCardModel> onSaved,
            Func<MissionCardModel, Task> onDelete,
            Func<MissionCardModel, Task> onFail,
            Func<MissionCardModel, Task> onRestore,
            List<string> availableTagsList,
            IActivityService activity,
            IUdmdService udmd,
            IMissionShareService missionShares,
            IClock clock,
            ITimeZoneService? timeZoneService = null,
            IAppNavigationService? navigation = null,
            IAppDialogService? dialogs = null)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
            _onFail = onFail ?? throw new ArgumentNullException(nameof(onFail));
            _onRestore = onRestore ?? throw new ArgumentNullException(nameof(onRestore));
            _timeZoneService = timeZoneService ?? new TimeZoneService();
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _missionShares = missionShares ?? throw new ArgumentNullException(nameof(missionShares));
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
            _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _detailsInteractions = new ActiveCardDetailsInteractionCoordinator(_navigation, _dialogs, _timeZoneService, _clock);
            _originalSnapshot = MissionEditSnapshot.Capture(_model);
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
            CaptureResourceImageCommand = new Command(async () => await CaptureResourceImageAsync());
            AddResourceImagesCommand = new Command(async () => await AddResourceImagesAsync());
            AddResourceFilesCommand = new Command(async () => await AddResourceFilesAsync());
            ShareCommand = new Command(async () => await ShareAsync(), () => CanShare);

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
                finally
                {
                    TryDeleteResourceCaptureCacheFile(src);
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
                CleanupResourceCaptureCacheFiles();
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

        private async Task CaptureResourceImageAsync()
        {
            if (!CanEdit)
                return;

            try
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await _dialogs.DisplayAlertAsync("Camera unavailable", "This device does not support camera capture.", "OK");
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "Resource photo"
                });

                if (photo == null)
                    return;

                var cachePath = CreateResourceCaptureCachePath(photo.FileName);

                await using (var source = await photo.OpenReadAsync())
                await using (var destination = File.Create(cachePath))
                {
                    await source.CopyToAsync(destination);
                }

                _resourceCaptureCachePaths.Add(cachePath);
                AddPendingResources(new[] { cachePath });
            }
            catch (OperationCanceledException)
            {
            }
            catch (PermissionException)
            {
                await _dialogs.DisplayAlertAsync("Camera permission", "Camera permission is required to capture this resource image.", "OK");
            }
            catch (Exception ex)
            {
                await _dialogs.DisplayAlertAsync("Resource photo failed", ex.Message, "OK");
            }
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

        private async Task ShareAsync()
        {
            if (!CanShare)
            {
                await _dialogs.DisplayAlertAsync("Share Mission", "Save the mission before sharing it.", "OK");
                return;
            }

            var missionToShare = _model;
            if (CanEdit)
            {
                var editedMission = await BuildEditedMissionFromFormAsync();
                if (editedMission == null)
                    return;

                missionToShare = editedMission;
            }

            try
            {
                await _missionShares.ShareMissionAsync(missionToShare);
            }
            catch (Exception ex)
            {
                await _dialogs.DisplayAlertAsync("Share failed", ex.Message, "OK");
                return;
            }

            var sharedWith = await _dialogs.DisplayPromptAsync(
                "Shared With",
                "Who did you share this mission with?",
                "Save",
                "Skip",
                "Name",
                initialValue: _model.SharedWith ?? "");

            if (string.IsNullOrWhiteSpace(sharedWith))
                return;

            _model.SharedWith = sharedWith.Trim();
            _onSaved(_model);
        }

        private void AddPendingResources(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                    ResourcesToAdd.Add(path);
            }
        }

        private static string CreateResourceCaptureCachePath(string? sourceFileName)
        {
            var originalName = Path.GetFileName(sourceFileName);
            if (string.IsNullOrWhiteSpace(originalName))
                originalName = "resource-photo.jpg";

            foreach (var c in Path.GetInvalidFileNameChars())
                originalName = originalName.Replace(c, '_');

            var name = Path.GetFileNameWithoutExtension(originalName);
            if (string.IsNullOrWhiteSpace(name))
                name = "resource-photo";

            var extension = Path.GetExtension(originalName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".jpg";

            return Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}_{name}{extension}");
        }

        private void CleanupResourceCaptureCacheFiles()
        {
            foreach (var path in _resourceCaptureCachePaths.ToList())
                TryDeleteResourceCaptureCacheFile(path);
        }

        private void TryDeleteResourceCaptureCacheFile(string path)
        {
            if (!_resourceCaptureCachePaths.Remove(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort cleanup for captured resource images that were queued before save.
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
            var editedMission = await BuildEditedMissionFromFormAsync();
            if (editedMission == null)
                return;

            ApplyEditableMissionFields(_model, editedMission);

            await SaveResourcesToDiskAsync();
            RefreshResourceCount();


            // CreatedDate stays as originally set (auto)
            // Status stays non-editable here
            // CompletedDate stays controlled by completion button

            var changed = !MissionEditSnapshot.Capture(_model).Equals(_originalSnapshot);

            _onSaved(_model);

            if (changed)
                await PromptShareUpdateIfNeededAsync("changes");

            await _navigation.PopAsync();
        }

        private async Task OnCancelAsync()
        {
            var completionAction = _model.IsFailed ? "Restore" : "Failed";
            var choice = await _dialogs.DisplayActionSheetAsync(
                _model.Title,
                "Cancel",
                null,
                "Delete",
                completionAction
            );

            if (choice == "Delete")
            {
                CleanupResourceCaptureCacheFiles();
                await _onDelete(_model);
                await _navigation.PopAsync();
            }
            else if (choice == "Failed")
            {
                CleanupResourceCaptureCacheFiles();
                await _onFail(_model);
                await _navigation.PopAsync();
            }
            else if (choice == "Restore")
            {
                CleanupResourceCaptureCacheFiles();
                await _onRestore(_model);
                await _navigation.PopAsync();
            }
        }

        private async Task<MissionCardModel?> BuildEditedMissionFromFormAsync()
        {
            var available = AvailableFromDate.Date + AvailableFromTime;
            var due = DueDate.Date + DueTime;

            if (due < available)
            {
                await _dialogs.DisplayAlertAsync("Invalid Dates", "Due By must be after Available From.", "OK");
                return null;
            }

            if (string.IsNullOrEmpty(Title))
            {
                await _dialogs.DisplayAlertAsync("Missing Title", "Please fill in the Title.", "OK");
                return null;
            }

            if (EstimatedTimeTs == TimeSpan.Zero)
            {
                await _dialogs.DisplayAlertAsync("Missing Est Time", "Please estimate the time required.", "OK");
                return null;
            }

            if (!double.TryParse(ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                await _dialogs.DisplayAlertAsync("Invalid Value", "Please enter a valid numeric value.", "OK");
                return null;
            }

            if (!double.TryParse(ValuePerMinText, NumberStyles.Float, CultureInfo.InvariantCulture, out var valuePerMin))
            {
                await _dialogs.DisplayAlertAsync("Invalid Value Per Minute", "Please enter a valid numeric value.", "OK");
                return null;
            }

            var mission = new MissionCardModel
            {
                Id = _model.Id,
                CardID = _model.CardID,
                MissionGuid = _model.MissionGuid,
                DisplayOrder = _model.DisplayOrder,
                Title = Title,
                Tags = Tags,
                Description = Description,
                SharedWith = _model.SharedWith,
                SubType = SelectedSubType,
                Value = value,
                CreatedDate = _model.CreatedDate,
                AvailableFromDate = available,
                DueDate = due,
                EventDate = HasEventDate ? EventDateValue.Date + EventTimeValue : null,
                EstCompletionTime = EstimatedTimeTs,
                ValuePerMinute = valuePerMin,
                Activity = _model.Activity
            };

            mission.ApplyCompletionState(_model.Status, _model.IsFailed, _model.CompletedDate);
            return mission;
        }

        private static void ApplyEditableMissionFields(MissionCardModel target, MissionCardModel source)
        {
            target.Title = source.Title;
            target.Tags = source.Tags;
            target.Description = source.Description;
            target.SubType = source.SubType;
            target.Value = source.Value;
            target.AvailableFromDate = source.AvailableFromDate;
            target.DueDate = source.DueDate;
            target.EventDate = source.EventDate;
            target.EstCompletionTime = source.EstCompletionTime;
            target.ValuePerMinute = source.ValuePerMinute;
        }

        private async Task PromptShareUpdateIfNeededAsync(string updateDescription)
        {
            if (string.IsNullOrWhiteSpace(_model.SharedWith))
                return;

            var send = await _dialogs.DisplayAlertAsync(
                "Share update?",
                $"This mission is shared with {_model.SharedWith}. Send the {updateDescription} now?",
                "Share",
                "Not now");

            if (!send)
                return;

            try
            {
                await _missionShares.ShareMissionAsync(_model);
            }
            catch (Exception ex)
            {
                await _dialogs.DisplayAlertAsync("Share failed", ex.Message, "OK");
            }
        }

        private sealed record MissionEditSnapshot(
            string Title,
            string Tags,
            string Description,
            MissionSubType SubType,
            string ValueText,
            string ValuePerMinuteText,
            DateTime AvailableFromDate,
            DateTime DueDate,
            DateTime? EventDate,
            string EstimatedTimeText)
        {
            public static MissionEditSnapshot Capture(MissionCardModel model)
            {
                return new MissionEditSnapshot(
                    model.Title ?? "",
                    model.Tags ?? "",
                    model.Description ?? "",
                    model.SubType,
                    model.Value.ToString("0.########", CultureInfo.InvariantCulture),
                    model.ValuePerMinute.ToString("0.########", CultureInfo.InvariantCulture),
                    model.AvailableFromDate,
                    model.DueDate,
                    model.EventDate,
                    model.EstCompletionTimeText ?? "");
            }
        }
    }
}
