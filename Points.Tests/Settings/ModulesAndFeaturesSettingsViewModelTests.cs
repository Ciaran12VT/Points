using Points.Global;
using Points.Models.DbModels;
using Points.Services.Notifications;
using Points.Services.Persistence;
using Points.ViewModels.Settings;
using Xunit;

namespace Points.Tests.Settings;

public sealed class ModulesAndFeaturesSettingsViewModelTests
{
    [Fact]
    public async Task Initialization_DisablesSaveUntilPersistedSettingsAreLoaded()
    {
        var load = new TaskCompletionSource<List<AcquiredSetting>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var settings = new RecordingSettingsService(load.Task);
        var viewModel = CreateViewModel(settings, Available());

        Assert.False(viewModel.IsInitialized);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        load.SetResult(Settings(parent: true, alertNoise: false));
        await viewModel.Initialization;

        Assert.True(viewModel.IsInitialized);
        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ParentOff_ClearsChildAndNormalizesInconsistentPersistedState()
    {
        var inconsistent = CreateViewModel(
            new RecordingSettingsService(Settings(parent: false, alertNoise: true)),
            Available());

        await inconsistent.Initialization;

        Assert.False(inconsistent.DeadAirNotificationEnabled);
        Assert.False(inconsistent.DeadAirAlertNoiseEnabled);

        var viewModel = CreateViewModel(
            new RecordingSettingsService(Settings(parent: true, alertNoise: true)),
            Available());
        await viewModel.Initialization;

        viewModel.DeadAirNotificationEnabled = false;

        Assert.False(viewModel.DeadAirAlertNoiseEnabled);
        Assert.False(viewModel.CanChangeDeadAirAlertNoise);
    }

    [Fact]
    public async Task BlockedNotificationAccess_PreservesSavedOnAndAllowsOnlyTurningItOff()
    {
        var availability = Blocked(ActiveCardNotificationAvailabilityStatus.ChannelDisabled);
        var viewModel = CreateViewModel(
            new RecordingSettingsService(Settings(parent: true, alertNoise: true)),
            availability);
        await viewModel.Initialization;

        Assert.True(viewModel.DeadAirAlertNoiseEnabled);
        Assert.True(viewModel.CanChangeDeadAirAlertNoise);
        Assert.True(viewModel.IsDeadAirAlertAvailabilityWarningVisible);
        Assert.StartsWith("Paused:", viewModel.DeadAirAlertAvailabilityMessage);

        viewModel.DeadAirAlertNoiseEnabled = false;

        Assert.False(viewModel.DeadAirAlertNoiseEnabled);
        Assert.False(viewModel.CanChangeDeadAirAlertNoise);

        viewModel.DeadAirAlertNoiseEnabled = true;

        Assert.False(viewModel.DeadAirAlertNoiseEnabled);
    }

    [Fact]
    public async Task ExternalAccessRevocation_DoesNotClearEnabledPreference()
    {
        var availability = Available();
        var viewModel = CreateViewModel(
            new RecordingSettingsService(Settings(parent: true, alertNoise: true)),
            availability);
        await viewModel.Initialization;

        availability.Availability = new ActiveCardNotificationAvailability(
            ActiveCardNotificationAvailabilityStatus.PermissionDenied);
        await viewModel.RefreshNotificationAvailabilityAsync();

        Assert.True(viewModel.DeadAirAlertNoiseEnabled);
        Assert.True(viewModel.CanChangeDeadAirAlertNoise);
        Assert.True(viewModel.CanOpenNotificationSettings);
    }

    [Fact]
    public async Task Save_WhenAccessIsBlocked_PreservesEnabledChildPreference()
    {
        var events = new List<string>();
        var viewModel = CreateViewModel(
            new RecordingSettingsService(
                Settings(parent: true, alertNoise: true),
                events),
            Blocked(ActiveCardNotificationAvailabilityStatus.AppNotificationsDisabled));
        await viewModel.Initialization;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(
            new[]
            {
                $"bool:{SettingKeys.DeadAirAlertNoiseEnabled}:false",
                $"bool:{SettingKeys.DeadAirNotificationEnabled}:true",
                $"bool:{SettingKeys.DeadAirAlertNoiseEnabled}:true"
            },
            events.Where(IsDeadAirSettingWrite));
        Assert.True(viewModel.DeadAirAlertNoiseEnabled);
    }

    [Fact]
    public async Task Save_UsesFailClosedOrderThenReconcilesBeforeHomeRefresh()
    {
        var events = new List<string>();
        var settings = new RecordingSettingsService(
            Settings(parent: false, alertNoise: false),
            events);
        var viewModel = CreateViewModel(
            settings,
            Available(),
            reconcile: () =>
            {
                events.Add("reconcile");
                return Task.CompletedTask;
            },
            onSaved: () =>
            {
                events.Add("home-refresh");
                return Task.CompletedTask;
            });
        await viewModel.Initialization;

        viewModel.DeadAirNotificationEnabled = true;
        viewModel.DeadAirAlertNoiseEnabled = true;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(
            new[]
            {
                $"bool:{SettingKeys.DeadAirAlertNoiseEnabled}:false",
                $"bool:{SettingKeys.DeadAirNotificationEnabled}:true",
                $"bool:{SettingKeys.DeadAirAlertNoiseEnabled}:true",
                "reconcile",
                "home-refresh"
            },
            events.Where(IsDeadAirSaveEvent));
    }

    [Fact]
    public async Task Save_WithParentOff_PersistsChildOffAndDoesNotReenableIt()
    {
        var events = new List<string>();
        var settings = new RecordingSettingsService(
            Settings(parent: true, alertNoise: true),
            events);
        var viewModel = CreateViewModel(settings, Available());
        await viewModel.Initialization;

        viewModel.DeadAirNotificationEnabled = false;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(
            new[]
            {
                $"bool:{SettingKeys.DeadAirAlertNoiseEnabled}:false",
                $"bool:{SettingKeys.DeadAirNotificationEnabled}:false"
            },
            events.Where(IsDeadAirSettingWrite));
    }

    [Fact]
    public async Task Save_PreventsConcurrentExecution()
    {
        var settings = new RecordingSettingsService(Settings(parent: true, alertNoise: false))
        {
            PauseFirstWrite = true
        };
        var reconcileCount = 0;
        var viewModel = CreateViewModel(
            settings,
            Available(),
            reconcile: () =>
            {
                reconcileCount++;
                return Task.CompletedTask;
            });
        await viewModel.Initialization;

        var firstSave = viewModel.SaveCommand.ExecuteAsync(null);
        await settings.FirstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.IsSaving);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        var secondSave = viewModel.SaveCommand.ExecuteAsync(null);
        settings.ReleaseFirstWrite.TrySetResult();
        await Task.WhenAll(firstSave, secondSave);

        Assert.Equal(1, settings.Writes.Count(x => x.StartsWith("bool:DashboardActive:")));
        Assert.Equal(1, reconcileCount);
    }

    [Fact]
    public async Task NullAvailability_IsUnavailableAndDoesNotOpenSettings()
    {
        var service = new NullActiveCardNotificationAvailabilityService();

        var availability = await service.GetAvailabilityAsync();
        await service.OpenNotificationSettingsAsync();

        Assert.Equal(
            ActiveCardNotificationAvailabilityStatus.UnsupportedPlatform,
            availability.Status);
        Assert.False(availability.IsAvailable);
        Assert.False(availability.CanOpenSettings);
    }

    private static ModulesAndFeaturesSettingsViewModel CreateViewModel(
        ISettingsService settings,
        IActiveCardNotificationAvailabilityService availability,
        Func<Task>? reconcile = null,
        Func<Task>? onSaved = null)
    {
        return new ModulesAndFeaturesSettingsViewModel(
            settings,
            availability,
            reconcile,
            onSaved);
    }

    private static FakeAvailabilityService Available()
    {
        return new FakeAvailabilityService
        {
            Availability = ActiveCardNotificationAvailability.Available
        };
    }

    private static FakeAvailabilityService Blocked(
        ActiveCardNotificationAvailabilityStatus status)
    {
        return new FakeAvailabilityService
        {
            Availability = new ActiveCardNotificationAvailability(status)
        };
    }

    private static List<AcquiredSetting> Settings(bool parent, bool alertNoise)
    {
        return new List<AcquiredSetting>
        {
            BoolSetting(SettingKeys.DeadAirNotificationEnabled, parent),
            BoolSetting(SettingKeys.DeadAirAlertNoiseEnabled, alertNoise)
        };
    }

    private static AcquiredSetting BoolSetting(string key, bool value)
    {
        return new AcquiredSetting
        {
            SettingKey = key,
            ValueType = SettingValueTypes.Bool,
            RawValue = value ? "true" : "false",
            BoolValue = value
        };
    }

    private static bool IsDeadAirSaveEvent(string value)
    {
        return IsDeadAirSettingWrite(value) ||
            value is "reconcile" or "home-refresh";
    }

    private static bool IsDeadAirSettingWrite(string value)
    {
        return value.StartsWith($"bool:{SettingKeys.DeadAirNotificationEnabled}:") ||
            value.StartsWith($"bool:{SettingKeys.DeadAirAlertNoiseEnabled}:");
    }

    private sealed class FakeAvailabilityService
        : IActiveCardNotificationAvailabilityService
    {
        public ActiveCardNotificationAvailability Availability { get; set; } =
            ActiveCardNotificationAvailability.Unknown;

        public int OpenSettingsCount { get; private set; }

        public Task<ActiveCardNotificationAvailability> GetAvailabilityAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Availability);
        }

        public Task OpenNotificationSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenSettingsCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSettingsService : ISettingsService
    {
        private readonly Task<List<AcquiredSetting>> _settingsTask;
        private readonly List<string> _events;
        private int _writeCount;

        public RecordingSettingsService(
            List<AcquiredSetting> settings,
            List<string>? events = null)
            : this(Task.FromResult(settings), events)
        {
        }

        public RecordingSettingsService(
            Task<List<AcquiredSetting>> settingsTask,
            List<string>? events = null)
        {
            _settingsTask = settingsTask;
            _events = events ?? new List<string>();
        }

        public bool PauseFirstWrite { get; init; }
        public TaskCompletionSource FirstWriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstWrite { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<string> Writes => _events;

        public Task<List<AcquiredSetting>> GetSettingsAsync() => _settingsTask;

        public Task SetStringSettingAsync(string settingKey, string value) =>
            RecordWriteAsync($"string:{settingKey}:{value}");

        public Task SetBoolSettingAsync(string settingKey, bool value) =>
            RecordWriteAsync($"bool:{settingKey}:{value.ToString().ToLowerInvariant()}");

        public Task SetIntSettingAsync(string settingKey, int value) =>
            RecordWriteAsync($"int:{settingKey}:{value}");

        public Task SetNullableIntSettingAsync(string settingKey, int? value) =>
            RecordWriteAsync($"nullable-int:{settingKey}:{value}");

        public Task SetDoubleSettingAsync(string settingKey, double value) =>
            RecordWriteAsync($"double:{settingKey}:{value}");

        private async Task RecordWriteAsync(string value)
        {
            _events.Add(value);

            if (!PauseFirstWrite || Interlocked.Increment(ref _writeCount) != 1)
                return;

            FirstWriteStarted.TrySetResult();
            await ReleaseFirstWrite.Task;
        }
    }
}
