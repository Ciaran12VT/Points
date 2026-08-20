using Points.Global;
using Points.Models;
using Points.Models.DbModels;
using Points.Services;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Tests.Time;
using Xunit;

namespace Points.Tests.Notifications;

public sealed class ActiveCardNotificationServiceTests
{
    [Fact]
    public async Task ReconcileAsync_WithActiveCard_PresentsCardWithoutReadingInactiveState()
    {
        var presenter = new RecordingPresenter();
        var settings = new FakeSettingsService(enabled: true);
        var activity = new FakeActivityService
        {
            LastClosedEndUtc = Utc(2026, 8, 19, 9)
        };
        var card = new TatCardModel { CardID = 42, Title = "Focus" };
        var service = CreateService(presenter, settings, activity);

        await service.ReconcileAsync(card);

        var request = Assert.Single(presenter.Requests);
        Assert.Equal(ActiveCardNotificationMode.ActiveCard, request.Mode);
        Assert.Same(card, request.ActiveCard);
        Assert.False(request.AlertNoiseRequested);
        Assert.Equal(0, settings.ReadCount);
        Assert.Equal(0, activity.LastClosedReadCount);
    }

    [Fact]
    public async Task ReconcileAsync_WhenInactiveAndDisabled_PresentsNone()
    {
        var presenter = new RecordingPresenter();
        var settings = new FakeSettingsService(enabled: false);
        var activity = new FakeActivityService();
        var service = CreateService(presenter, settings, activity);

        await service.ReconcileAsync(activeCard: null);

        Assert.Equal(ActiveCardNotificationMode.None, Assert.Single(presenter.Requests).Mode);
        Assert.Equal(1, settings.ReadCount);
        Assert.Equal(0, activity.LastClosedReadCount);
    }

    [Fact]
    public async Task ReconcileAsync_WhenDeadAirEnabled_UsesLatestClosedActivityEnd()
    {
        var lastClosedEndUtc = Utc(2026, 8, 18, 4, 30);
        var presenter = new RecordingPresenter();
        var service = CreateService(
            presenter,
            new FakeSettingsService(enabled: true),
            new FakeActivityService { LastClosedEndUtc = lastClosedEndUtc });

        await service.ReconcileAsync(activeCard: null);

        var request = Assert.Single(presenter.Requests);
        Assert.Equal(ActiveCardNotificationMode.DeadAir, request.Mode);
        Assert.Equal(lastClosedEndUtc, request.DeadAirStartedAtUtc);
        Assert.False(request.AlertNoiseRequested);
    }

    [Fact]
    public async Task ReconcileAsync_WhenBothDeadAirSettingsEnabled_RequestsAlertNoise()
    {
        var presenter = new RecordingPresenter();
        var service = CreateService(
            presenter,
            new FakeSettingsService(enabled: true, alertNoiseEnabled: true),
            new FakeActivityService { LastClosedEndUtc = Utc(2026, 8, 19, 11) });

        await service.ReconcileAsync(activeCard: null);

        var request = Assert.Single(presenter.Requests);
        Assert.Equal(ActiveCardNotificationMode.DeadAir, request.Mode);
        Assert.True(request.AlertNoiseRequested);
    }

    [Fact]
    public async Task ReconcileAsync_WhenOnlyAlertNoiseEnabled_PresentsNone()
    {
        var presenter = new RecordingPresenter();
        var service = CreateService(
            presenter,
            new FakeSettingsService(enabled: false, alertNoiseEnabled: true),
            new FakeActivityService());

        await service.ReconcileAsync(activeCard: null);

        var request = Assert.Single(presenter.Requests);
        Assert.Equal(ActiveCardNotificationMode.None, request.Mode);
        Assert.False(request.AlertNoiseRequested);
    }

    [Fact]
    public async Task ReconcileAsync_WhenNoActivityHistory_UsesCurrentLocalMidnight()
    {
        var utcNow = Utc(2026, 8, 19, 8);
        var localNow = new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+02-test", TimeSpan.FromHours(2), "UTC+02", "UTC+02");
        var presenter = new RecordingPresenter();
        var service = new ActiveCardNotificationService(
            presenter,
            new FakeSettingsService(enabled: true),
            new FakeActivityService(),
            new FixedClock(utcNow, localNow),
            new FixedZoneTimeZoneService(zone));

        await service.ReconcileAsync(activeCard: null);

        Assert.Equal(
            Utc(2026, 8, 18, 22),
            Assert.Single(presenter.Requests).DeadAirStartedAtUtc);
    }

    [Fact]
    public async Task ReconcileAsync_ClampsFutureActivityEndToNow()
    {
        var nowUtc = Utc(2026, 8, 19, 12);
        var presenter = new RecordingPresenter();
        var service = CreateService(
            presenter,
            new FakeSettingsService(enabled: true),
            new FakeActivityService { LastClosedEndUtc = nowUtc.AddMinutes(5) },
            nowUtc: nowUtc);

        await service.ReconcileAsync(activeCard: null);

        Assert.Equal(nowUtc, Assert.Single(presenter.Requests).DeadAirStartedAtUtc);
    }

    [Fact]
    public async Task ReconcileAsync_ClampsNonUtcActivityEndToNow()
    {
        var nowUtc = Utc(2026, 8, 19, 12);
        var invalidEnd = new DateTime(2026, 8, 19, 11, 30, 0, DateTimeKind.Unspecified);
        var presenter = new RecordingPresenter();
        var service = CreateService(
            presenter,
            new FakeSettingsService(enabled: true),
            new FakeActivityService { LastClosedEndUtc = invalidEnd },
            nowUtc: nowUtc);

        await service.ReconcileAsync(activeCard: null);

        Assert.Equal(nowUtc, Assert.Single(presenter.Requests).DeadAirStartedAtUtc);
    }

    [Fact]
    public async Task ReconcileAsync_SerializesConcurrentPresenterCalls()
    {
        var presenter = new BlockingPresenter();
        var service = CreateService(
            presenter,
            new FakeSettingsService(enabled: false),
            new FakeActivityService());
        var firstCard = new TatCardModel { CardID = 1, Title = "First" };
        var secondCard = new TatCardModel { CardID = 2, Title = "Second" };

        var first = service.ReconcileAsync(firstCard);
        await presenter.FirstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var second = service.ReconcileAsync(secondCard);
        var prematureSecond = await Task.WhenAny(
            presenter.SecondEntered.Task,
            Task.Delay(TimeSpan.FromMilliseconds(100)));
        Assert.NotSame(presenter.SecondEntered.Task, prematureSecond);

        presenter.ReleaseFirst.TrySetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, presenter.MaximumConcurrency);
        Assert.Equal(
            new long[] { 1, 2 },
            presenter.Requests.Select(request => request.ActiveCard!.CardID));
    }

    [Fact]
    public async Task ReconcileAsync_DelayedIdleLookupCannotOverwriteLaterActiveRequest()
    {
        var lookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLookup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var presenter = new RecordingPresenter();
        var activity = new FakeActivityService
        {
            LastClosedEndUtc = Utc(2026, 8, 19, 11),
            LastClosedReadStarted = lookupStarted,
            ReleaseLastClosedRead = releaseLookup
        };
        var service = CreateService(
            presenter,
            new FakeSettingsService(enabled: true),
            activity);
        var activeCard = new TatCardModel { CardID = 42, Title = "Focus" };

        var idleReconciliation = service.ReconcileAsync(activeCard: null);
        await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var activeReconciliation = service.ReconcileAsync(activeCard);
        releaseLookup.TrySetResult();
        await Task.WhenAll(idleReconciliation, activeReconciliation);

        Assert.Equal(
            new[] { ActiveCardNotificationMode.DeadAir, ActiveCardNotificationMode.ActiveCard },
            presenter.Requests.Select(request => request.Mode));
    }

    private static ActiveCardNotificationService CreateService(
        IActiveCardNotificationPresenter presenter,
        ISettingsService settings,
        IActivityService activity,
        DateTime? nowUtc = null)
    {
        var utc = nowUtc ?? Utc(2026, 8, 19, 12);
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

        return new ActiveCardNotificationService(
            presenter,
            settings,
            activity,
            new FixedClock(utc, local),
            new FixedZoneTimeZoneService(TimeZoneInfo.Utc));
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow, DateTime localNow)
        {
            UtcNow = utcNow;
            LocalNow = localNow;
        }

        public DateTime UtcNow { get; }
        public DateTime LocalNow { get; }
        public DateTimeOffset UtcNowOffset => new(UtcNow);
    }

    private sealed class RecordingPresenter : IActiveCardNotificationPresenter
    {
        public List<ActiveCardNotificationRequest> Requests { get; } = new();

        public Task PresentAsync(
            ActiveCardNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingPresenter : IActiveCardNotificationPresenter
    {
        private int _concurrency;

        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ActiveCardNotificationRequest> Requests { get; } = new();
        public int MaximumConcurrency { get; private set; }

        public async Task PresentAsync(
            ActiveCardNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            var concurrency = Interlocked.Increment(ref _concurrency);
            MaximumConcurrency = Math.Max(MaximumConcurrency, concurrency);

            try
            {
                Requests.Add(request);

                if (Requests.Count == 1)
                {
                    FirstEntered.TrySetResult();
                    await ReleaseFirst.Task.WaitAsync(cancellationToken);
                }
                else
                {
                    SecondEntered.TrySetResult();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _concurrency);
            }
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly bool _enabled;
        private readonly bool _alertNoiseEnabled;

        public FakeSettingsService(bool enabled, bool alertNoiseEnabled = false)
        {
            _enabled = enabled;
            _alertNoiseEnabled = alertNoiseEnabled;
        }

        public int ReadCount { get; private set; }

        public Task<List<AcquiredSetting>> GetSettingsAsync()
        {
            ReadCount++;
            return Task.FromResult(new List<AcquiredSetting>
            {
                new()
                {
                    SettingKey = SettingKeys.DeadAirNotificationEnabled,
                    ValueType = SettingValueTypes.Bool,
                    RawValue = _enabled ? "true" : "false",
                    BoolValue = _enabled
                },
                new()
                {
                    SettingKey = SettingKeys.DeadAirAlertNoiseEnabled,
                    ValueType = SettingValueTypes.Bool,
                    RawValue = _alertNoiseEnabled ? "true" : "false",
                    BoolValue = _alertNoiseEnabled
                }
            });
        }

        public Task SetStringSettingAsync(string settingKey, string value) => Task.CompletedTask;
        public Task SetBoolSettingAsync(string settingKey, bool value) => Task.CompletedTask;
        public Task SetIntSettingAsync(string settingKey, int value) => Task.CompletedTask;
        public Task SetNullableIntSettingAsync(string settingKey, int? value) => Task.CompletedTask;
        public Task SetDoubleSettingAsync(string settingKey, double value) => Task.CompletedTask;
    }

    private sealed class FakeActivityService : IActivityService
    {
        public DateTime? LastClosedEndUtc { get; init; }
        public TaskCompletionSource? LastClosedReadStarted { get; init; }
        public TaskCompletionSource? ReleaseLastClosedRead { get; init; }
        public int LastClosedReadCount { get; private set; }

        public Task<ActivityModel?> GetCurrentActiveActivityAsync() => Task.FromResult<ActivityModel?>(null);

        public Task<ToggleActivityModelResult> ToggleActivityAsync(
            long cardId,
            DateTime utcNow,
            string valueRateName,
            double valuePerMinute) => throw new NotSupportedException();

        public Task<bool> HasActivityOverlapAsync(
            int excludeActivityId,
            DateTime candidateStart,
            DateTime? candidateEnd) => throw new NotSupportedException();

        public Task<ActivityUpdateResult> UpsertActivitiesAsync(
            List<ActivityModel> activities,
            long? replaceCardId = null) => throw new NotSupportedException();

        public Task<DateTime?> GetCurrentOpenActivityStartUtcAsync(long cardId) =>
            Task.FromResult<DateTime?>(null);

        public async Task<DateTime?> GetLastClosedActivityEndUtcAsync()
        {
            LastClosedReadCount++;
            LastClosedReadStarted?.TrySetResult();

            if (ReleaseLastClosedRead != null)
                await ReleaseLastClosedRead.Task;

            return LastClosedEndUtc;
        }

        public Task AddRepForStep(int scCardStepID, DateTime repTime, double stepValue) =>
            throw new NotSupportedException();
    }
}
