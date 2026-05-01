using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Views.Shared;
using Points.Views.Udmd;
using Points.Views.Schedules;

namespace Points.ViewModels.Shared
{
    internal sealed class ActiveCardDetailsInteractionCoordinator
    {
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly ITimeZoneService? _timeZoneService;
        private readonly IClock? _clock;

        public ActiveCardDetailsInteractionCoordinator(
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            ITimeZoneService? timeZoneService = null,
            IClock? clock = null)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _timeZoneService = timeZoneService;
            _clock = clock;
        }

        public Task<string?> PickTagsAsync(
            IEnumerable<string> allTags,
            string? currentTags,
            bool isReadOnly = false)
        {
            return PickValuesAsync("Select Tags", allTags, currentTags, isReadOnly);
        }

        public async Task<string?> PickValuesAsync(
            string title,
            IEnumerable<string> values,
            string? currentValues,
            bool isReadOnly = true)
        {
            var initial = (currentValues ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var page = new MultiSelectPickerPage(
                title,
                values,
                initial,
                _navigation,
                isReadOnly);

            await _navigation.PushAsync(page);

            var result = await page.Result;
            return result == null ? null : string.Join(", ", result);
        }

        public async Task EditActiveTimeAsync(
            TatCardModel model,
            IActivityService activity,
            IUdmdService udmd)
        {
            await EditActiveTimeAsync(
                model.Activity,
                model.CardID,
                edited => model.Activity = edited,
                activity,
                udmd);
        }

        public async Task EditActiveTimeAsync(
            MissionCardModel model,
            IActivityService activity,
            IUdmdService udmd)
        {
            await EditActiveTimeAsync(
                model.Activity,
                model.CardID,
                edited => model.Activity = edited,
                activity,
                udmd);
        }

        private async Task EditActiveTimeAsync(
            List<ActivityModel> currentActivity,
            long cardId,
            Action<List<ActivityModel>> applyEdited,
            IActivityService activity,
            IUdmdService udmd)
        {
            var tcs = new TaskCompletionSource<List<ActivityModel>>();

            var page = new EditActiveTimePage(
                currentActivity,
                tcs,
                activity,
                udmd,
                RequireTimeZoneService(),
                navigation: _navigation,
                dialogs: _dialogs);

            await _navigation.PushAsync(page);

            try
            {
                var edited = await tcs.Task;

                if (cardId > 0)
                {
                    var result = await activity.UpsertActivitiesAsync(edited, cardId);
                    if (!result.Success)
                    {
                        await _dialogs.DisplayAlertAsync("Active time not saved", result.Message, "OK");
                        return;
                    }
                }

                applyEdited(edited);
            }
            catch (TaskCanceledException)
            {
            }
        }

        public async Task EditSchedulesAsync(
            long cardId,
            System.Collections.ObjectModel.ObservableCollection<CardSchedule> schedules,
            Action onChanged,
            Action<string> showError,
            string notPersistedMessage)
        {
            if (cardId <= 0)
            {
                showError(notPersistedMessage);
                return;
            }

            await _navigation.PushAsync(
                new CardSchedulesPage(
                    cardId: cardId,
                    schedules: schedules,
                    onChanged: onChanged,
                    navigation: _navigation,
                    dialogs: _dialogs,
                    clock: RequireClock()));
        }

        public async Task EditLocksAsync(
            long cardId,
            List<LockModel> locks,
            ILockService locksService,
            List<DependencyTaskOption> dependencyOptions,
            Action onChanged,
            Action<string> showError)
        {
            if (cardId <= 0)
            {
                showError("Please tap OK to save the tracker first, then add locks.");
                return;
            }

            await _navigation.PushAsync(
                new EditLocksPage(
                    cardId: cardId,
                    locks: locks,
                    locksService: locksService,
                    dependencyOptions: dependencyOptions,
                    onChanged: onChanged,
                    navigation: _navigation,
                    dialogs: _dialogs,
                    clock: RequireClock()));
        }

        public async Task EditUdmdAsync(
            long cardId,
            IUdmdService udmd,
            Action<string> showError,
            string notPersistedMessage = "Please save the card before configuring metadata fields.")
        {
            if (cardId <= 0)
            {
                showError(notPersistedMessage);
                return;
            }

            await _navigation.PushAsync(new UdmdConfigPage(cardId, udmd));
        }

        public async Task<(bool WasCancelled, TimeSpan? Target)> PickActiveTimeTargetAsync(TimeSpan? initial)
        {
            var page = new DurationPickerPage(initial, _navigation);
            await _navigation.PushModalAsync(new NavigationPage(page));

            var result = await page.Result;
            return (page.WasCancelled, result);
        }

        public async Task<TimeSpan?> PickDurationAsync(TimeSpan? initial = null)
        {
            var page = new DurationPickerPage(initial, _navigation);
            await _navigation.PushModalAsync(new NavigationPage(page));
            return await page.Result;
        }

        public async Task<IReadOnlyList<string>> PickFilePathsAsync(
            string title,
            FilePickerFileType? fileTypes = null)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = title,
                    FileTypes = fileTypes
                };

                var results = await FilePicker.Default.PickMultipleAsync(options);
                return (results ?? Enumerable.Empty<FileResult>())
                    .Select(x => x.FullPath)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .ToList();
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<string>();
            }
        }

        public async Task<string?> PickFilePathAsync(
            string title,
            FilePickerFileType? fileTypes = null)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = title,
                    FileTypes = fileTypes
                });

                return string.IsNullOrWhiteSpace(result?.FullPath)
                    ? null
                    : result.FullPath;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private ITimeZoneService RequireTimeZoneService()
        {
            return _timeZoneService
                ?? throw new InvalidOperationException("Active-time editing requires an ITimeZoneService.");
        }

        private IClock RequireClock()
        {
            return _clock
                ?? throw new InvalidOperationException("Schedule and lock editing requires an IClock.");
        }
    }
}
